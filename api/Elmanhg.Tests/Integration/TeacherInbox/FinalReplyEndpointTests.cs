using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.TeacherInbox.TeacherInboxTestData;

namespace Elmanhg.Tests.Integration.TeacherInbox;

public sealed class FinalReplyEndpointTests(ApiFactory factory)
{
    private const string ReplyText = "The unit is the newton.";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Reply_AfterFollowUp_ClosesThreadAndOffersOnlyRating()
    {
        var subjectId = await SeedSubjectAsync();
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var (teacher, client) = await SignedInTeacherForAsync(factory, subjectId);
        var thread = await SeedThreadAsync(factory, new TeacherThreadBuilder().ForStudent(student.Id).WithContext(ContextFor(subjectId)).AnsweredBy(teacher.Id).FollowedUp().Build());

        using var response = await client.PostAsJsonAsync($"{Route}/{thread.Id}/replies", new { text = ReplyText }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("status").GetString().Should().Be("Closed");
        var stored = await ReadThreadAsync(factory, thread.Id);
        stored.Status.Should().Be(TeacherThreadStatus.Closed);
        stored.ClosedAt.Should().NotBeNull();
        using var studentClient = await ScopeTestData.SignedInClientAsync(factory, student, CancellationToken);
        var studentView = await studentClient.GetFromJsonAsync<JsonElement>($"/api/teacher-threads/{thread.Id}", CancellationToken);
        (studentView.GetProperty("canFollowUp").GetBoolean(), studentView.GetProperty("canRate").GetBoolean()).Should().Be((false, true));
    }

    [Fact]
    public async Task Reply_ToClosedThread_Returns409NotAwaitingReply()
    {
        var subjectId = await SeedSubjectAsync();
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var (teacher, client) = await SignedInTeacherForAsync(factory, subjectId);
        var thread = await SeedThreadAsync(factory, new TeacherThreadBuilder().ForStudent(student.Id).WithContext(ContextFor(subjectId)).AnsweredBy(teacher.Id).FinalReplied().Build());

        using var response = await client.PostAsJsonAsync($"{Route}/{thread.Id}/replies", new { text = ReplyText }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("code").GetString().Should().Be("TEACHER_THREAD_NOT_AWAITING_REPLY");
        (await ReadThreadAsync(factory, thread.Id)).Messages.Should().HaveCount(4);
    }

    private Task<Guid> SeedSubjectAsync() => ScopeTestData.SeedSubjectAsync(factory, $"Physics {Guid.NewGuid():N}", CancellationToken);
}
