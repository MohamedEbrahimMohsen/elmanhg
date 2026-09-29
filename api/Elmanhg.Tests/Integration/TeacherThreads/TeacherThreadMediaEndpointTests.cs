using Elmanhg.Application.TeacherInbox.TranscribeVoiceDraft;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.TeacherThreads.TeacherThreadTestData;
using InboxData = Elmanhg.Tests.Integration.TeacherInbox.TeacherInboxTestData;

namespace Elmanhg.Tests.Integration.TeacherThreads;

public sealed class TeacherThreadMediaEndpointTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_OwningStudent_ServesPrivateImage()
    {
        var (_, imageUrl, owner) = await SeedThreadWithImageAsync();

        using var response = await owner.GetAsync(imageUrl, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsByteArrayAsync(CancellationToken)).Should().Equal(PngBytes);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle().Which.Should().Be("nosniff");
        response.Headers.CacheControl!.Private.Should().BeTrue();
    }

    [Fact]
    public async Task Get_Anonymous_Returns404()
    {
        var (_, imageUrl, _) = await SeedThreadWithImageAsync();
        using var anonymous = AuthTestClient.Create(factory);

        using var response = await anonymous.GetAsync(imageUrl, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_OtherStudent_Returns404()
    {
        var (_, imageUrl, _) = await SeedThreadWithImageAsync();
        var (_, other) = await SignedInAskTeacherStudentAsync(factory);

        using var response = await other.GetAsync(imageUrl, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_TeacherScopedToTheSubject_ServesImage()
    {
        var (subjectId, imageUrl, _) = await SeedThreadWithImageAsync();
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        await ScopeTestData.AssignAsync(factory, teacher.Id, subjectId, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, CancellationToken);

        using var response = await client.GetAsync(imageUrl, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsByteArrayAsync(CancellationToken)).Should().Equal(PngBytes);
    }

    [Fact]
    public async Task Get_TeacherOutsideTheSubject_Returns404()
    {
        var (_, imageUrl, _) = await SeedThreadWithImageAsync();
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, CancellationToken);

        using var response = await client.GetAsync(imageUrl, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_Admin_ServesImage()
    {
        var (_, imageUrl, _) = await SeedThreadWithImageAsync();
        var admin = await ScopeTestData.SeedAdminAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, admin, CancellationToken);

        using var response = await client.GetAsync(imageUrl, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/api/media//teacher-threads/{0}")]
    [InlineData("/api/media/Teacher-Threads/{0}")]
    [InlineData("/api/media/teacher-threads./{0}")]
    [InlineData("/api/media/teacher-threads%5C{0}")]
    [InlineData("/api/media/lessons/..%5Cteacher-threads%5C{0}")]
    public async Task Get_NonCanonicalPathAnonymously_Returns404(string pathFormat)
    {
        var (_, imageUrl, _) = await SeedThreadWithImageAsync();
        using var anonymous = AuthTestClient.Create(factory);

        using var response = await anonymous.GetAsync(string.Format(pathFormat, Path.GetFileName(imageUrl)), CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_OwningStudentVoiceReply_ServesAudio()
    {
        var (audioUrl, owner, _) = await SeedVoiceReplyAsync(send: true);

        using var response = await owner.GetAsync(audioUrl, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsByteArrayAsync(CancellationToken)).Should().Equal(InboxData.WebmBytes);
        response.Content.Headers.ContentType!.MediaType.Should().Be("audio/webm");
        response.Headers.CacheControl!.Private.Should().BeTrue();
    }

    [Fact]
    public async Task Get_OtherStudentVoiceReply_Returns404()
    {
        var (audioUrl, _, _) = await SeedVoiceReplyAsync(send: true);
        var (_, other) = await SignedInAskTeacherStudentAsync(factory);

        using var response = await other.GetAsync(audioUrl, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_UnsentDraftAudio_Returns404ForItsTeacher()
    {
        var (audioUrl, _, teacher) = await SeedVoiceReplyAsync(send: false);

        using var response = await teacher.GetAsync(audioUrl, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<(string AudioUrl, HttpClient Owner, HttpClient Teacher)> SeedVoiceReplyAsync(bool send)
    {
        var subjectId = await ScopeTestData.SeedSubjectAsync(factory, $"Physics {Guid.NewGuid():N}", CancellationToken);
        var (student, owner) = await SignedInAskTeacherStudentAsync(factory);
        var (teacher, client) = await InboxData.SignedInTeacherForAsync(factory, subjectId);
        var thread = await InboxData.SeedThreadAsync(factory, new TeacherThreadBuilder().ForStudent(student.Id).WithContext(InboxData.ContextFor(subjectId)).ClaimedBy(teacher.Id).Build());
        using var recorded = await client.PostAsync($"{InboxData.Route}/{thread.Id}/voice-drafts", InboxData.VoiceForm(InboxData.WebmBytes), CancellationToken);
        var draftId = (await recorded.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("id").GetGuid();
        if (send)
        {
            using var scope = factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new TranscribeVoiceDraftCommand(draftId), CancellationToken);
            using var sent = await client.PostAsJsonAsync($"{InboxData.Route}/{thread.Id}/voice-replies", new { draftId, text = "Voice transcript." }, CancellationToken);
            sent.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var stored = await ReadDraftUrlAsync(draftId);
        return (stored, owner, client);
    }

    private async Task<string> ReadDraftUrlAsync(Guid draftId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (await context.TeacherVoiceDrafts.FindAsync([draftId], CancellationToken))!.AudioUrl;
    }

    private async Task<(Guid SubjectId, string ImageUrl, HttpClient Owner)> SeedThreadWithImageAsync()
    {
        var (subjectId, lessonId) = await SeedPublishedLessonAsync(factory);
        var (_, owner) = await SignedInAskTeacherStudentAsync(factory);
        using var response = await owner.PostAsync(Route, QuestionForm("Why is F = ma?", lessonId: lessonId, imageFileName: "photo.png"), CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);
        return (subjectId, body.GetProperty("messages")[0].GetProperty("imageUrl").GetString()!, owner);
    }
}
