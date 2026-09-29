using Elmanhg.Application.Shared.Storage;
using Elmanhg.Application.TeacherInbox.TranscribeVoiceDraft;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Infrastructure.AiService;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.TeacherInbox.TeacherInboxTestData;

namespace Elmanhg.Tests.Integration.TeacherInbox;

public sealed class VoiceReplyEndpointTests(ApiFactory factory)
{
    private const string EditedText = "The corrected voice transcript.";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PostVoiceDraft_Claimer_StoresAudioAndReturnsPendingDraft()
    {
        var (_, client, thread, _) = await SeedClaimedThreadAsync();

        using var response = await client.PostAsync($"{Route}/{thread.Id}/voice-drafts", VoiceForm(WebmBytes), CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);
        body.GetProperty("status").GetString().Should().Be("Pending");
        var draft = await ReadDraftAsync(body.GetProperty("id").GetGuid());
        (draft.Status, draft.AudioDurationSeconds).Should().Be((TeacherVoiceDraftStatus.Pending, 12));
        draft.AudioKey.Should().MatchRegex("^teacher-threads/[0-9a-f]{32}\\.webm$");
        using var scope = factory.Services.CreateScope();
        await using var file = await scope.ServiceProvider.GetRequiredService<IFileStorage>().OpenReadAsync(draft.AudioKey, CancellationToken);
        file!.Length.Should().Be(WebmBytes.Length);
    }

    [Fact]
    public async Task PostVoiceDraft_UnclaimedThread_Returns409NotClaimed()
    {
        var subjectId = await SeedSubjectAsync();
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var (_, client) = await SignedInTeacherForAsync(factory, subjectId);
        var thread = await SeedThreadAsync(factory, new TeacherThreadBuilder().ForStudent(student.Id).WithContext(ContextFor(subjectId)).Build());

        using var response = await client.PostAsync($"{Route}/{thread.Id}/voice-drafts", VoiceForm(WebmBytes), CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadCodeAsync(response)).Should().Be("TEACHER_THREAD_NOT_CLAIMED");
    }

    [Fact]
    public async Task PostVoiceDraft_InvalidAudio_Returns422TypeInvalid()
    {
        var (_, client, thread, _) = await SeedClaimedThreadAsync();

        using var response = await client.PostAsync($"{Route}/{thread.Id}/voice-drafts", VoiceForm([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]), CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Contain("TEACHER_VOICE_AUDIO_TYPE_INVALID");
    }

    [Fact]
    public async Task PostVoiceDraft_Student_Returns403()
    {
        var (_, _, thread, student) = await SeedClaimedThreadAsync();
        using var client = await ScopeTestData.SignedInClientAsync(factory, student, CancellationToken);

        using var response = await client.PostAsync($"{Route}/{thread.Id}/voice-drafts", VoiceForm(WebmBytes), CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostVoiceDraft_Anonymous_Returns401()
    {
        using var client = AuthTestClient.Create(factory);

        using var response = await client.PostAsync($"{Route}/{Guid.NewGuid()}/voice-drafts", VoiceForm(WebmBytes), CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetVoiceDraft_AfterTranscription_ReturnsReadyTranscript()
    {
        var (_, client, thread, _) = await SeedClaimedThreadAsync();
        var draftId = await RecordAndTranscribeAsync(client, thread.Id);

        var body = await client.GetFromJsonAsync<JsonElement>($"{Route}/{thread.Id}/voice-drafts/{draftId}", CancellationToken);

        (body.GetProperty("status").GetString(), body.GetProperty("transcript").GetString()).Should().Be(("Ready", FakeAiTranscriptionClient.FakeTranscript));
    }

    [Fact]
    public async Task GetVoiceDraft_OtherTeacher_Returns404()
    {
        var (subjectId, client, thread, _) = await SeedClaimedThreadAsync();
        var draftId = await RecordAsync(client, thread.Id);
        var (_, otherClient) = await SignedInTeacherForAsync(factory, subjectId);

        using var response = await otherClient.GetAsync($"{Route}/{thread.Id}/voice-drafts/{draftId}", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("TEACHER_VOICE_DRAFT_NOT_FOUND");
    }

    [Fact]
    public async Task SendVoiceReply_ReadyDraft_AnswersThreadWithVoiceMessage()
    {
        var (_, client, thread, _) = await SeedClaimedThreadAsync();
        var draftId = await RecordAndTranscribeAsync(client, thread.Id);

        using var response = await client.PostAsJsonAsync($"{Route}/{thread.Id}/voice-replies", new { draftId, text = EditedText }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stored = await ReadThreadAsync(factory, thread.Id);
        var draft = await ReadDraftAsync(draftId);
        stored.Status.Should().Be(TeacherThreadStatus.Answered);
        var message = stored.Messages.Single(x => x.Kind == TeacherMessageKind.Voice);
        (message.TranscriptFinal, message.Text, message.AudioUrl, message.AudioDurationSeconds).Should().Be((true, EditedText, draft.AudioUrl, (int?)12));
        (draft.Status, draft.SentMessageId).Should().Be((TeacherVoiceDraftStatus.Sent, (Guid?)message.Id));
    }

    [Fact]
    public async Task SendVoiceReply_PendingDraft_Returns409NotReady()
    {
        var (_, client, thread, _) = await SeedClaimedThreadAsync();
        var draftId = await RecordAsync(client, thread.Id);

        using var response = await client.PostAsJsonAsync($"{Route}/{thread.Id}/voice-replies", new { draftId, text = EditedText }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadCodeAsync(response)).Should().Be("TEACHER_VOICE_DRAFT_NOT_READY");
        (await ReadThreadAsync(factory, thread.Id)).Messages.Should().HaveCount(1);
    }

    [Fact]
    public async Task SendVoiceReply_BlankText_Returns422()
    {
        var (_, client, thread, _) = await SeedClaimedThreadAsync();
        var draftId = await RecordAndTranscribeAsync(client, thread.Id);

        using var response = await client.PostAsJsonAsync($"{Route}/{thread.Id}/voice-replies", new { draftId, text = "  " }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Contain("TEACHER_THREAD_REPLY_TEXT_REQUIRED");
    }

    [Fact]
    public async Task GetMyThread_VoiceReply_ReturnsAudioUrlAndTranscript()
    {
        var (_, client, thread, student) = await SeedClaimedThreadAsync();
        var draftId = await RecordAndTranscribeAsync(client, thread.Id);
        using var sent = await client.PostAsJsonAsync($"{Route}/{thread.Id}/voice-replies", new { draftId, text = EditedText }, CancellationToken);
        using var studentClient = await ScopeTestData.SignedInClientAsync(factory, student, CancellationToken);

        var body = await studentClient.GetFromJsonAsync<JsonElement>($"/api/teacher-threads/{thread.Id}", CancellationToken);

        var message = body.GetProperty("messages").EnumerateArray().Single(x => x.GetProperty("kind").GetString() == "Voice");
        (message.GetProperty("kind").GetString(), message.GetProperty("text").GetString(), message.GetProperty("audioDurationSeconds").GetInt32()).Should().Be(("Voice", EditedText, 12));
        message.GetProperty("audioUrl").GetString().Should().MatchRegex("^/api/media/teacher-threads/[0-9a-f]{32}\\.webm$");
    }

    [Fact]
    public async Task GetVoiceSettings_Teacher_ReturnsLimits()
    {
        var (_, client) = await SignedInTeacherForAsync(factory, await SeedSubjectAsync());

        var body = await client.GetFromJsonAsync<JsonElement>($"{Route}/voice-settings", CancellationToken);

        (body.GetProperty("maxDurationSeconds").GetInt32(), body.GetProperty("maxSizeInMb").GetInt32()).Should().Be((180, 5));
    }

    [Fact]
    public async Task GetVoiceSettings_Student_Returns403()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, student, CancellationToken);

        using var response = await client.GetAsync($"{Route}/voice-settings", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<(Guid SubjectId, HttpClient Client, TeacherThread Thread, User Student)> SeedClaimedThreadAsync()
    {
        var subjectId = await SeedSubjectAsync();
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var (teacher, client) = await SignedInTeacherForAsync(factory, subjectId);
        var thread = await SeedThreadAsync(factory, new TeacherThreadBuilder().ForStudent(student.Id).WithContext(ContextFor(subjectId)).ClaimedBy(teacher.Id).Build());
        return (subjectId, client, thread, student);
    }

    private async Task<Guid> RecordAsync(HttpClient client, Guid threadId)
    {
        using var response = await client.PostAsync($"{Route}/{threadId}/voice-drafts", VoiceForm(WebmBytes), CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("id").GetGuid();
    }

    private async Task<Guid> RecordAndTranscribeAsync(HttpClient client, Guid threadId)
    {
        var draftId = await RecordAsync(client, threadId);
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(new TranscribeVoiceDraftCommand(draftId), CancellationToken);
        return draftId;
    }

    private async Task<TeacherVoiceDraft> ReadDraftAsync(Guid draftId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().TeacherVoiceDrafts.AsNoTracking().SingleAsync(x => x.Id == draftId, CancellationToken);
    }

    private Task<Guid> SeedSubjectAsync() => ScopeTestData.SeedSubjectAsync(factory, $"Physics {Guid.NewGuid():N}", CancellationToken);

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response) => (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("code").GetString();
}
