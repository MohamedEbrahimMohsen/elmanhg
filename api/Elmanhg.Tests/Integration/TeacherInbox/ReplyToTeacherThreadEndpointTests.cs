using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.TeacherInbox.TeacherInboxTestData;

namespace Elmanhg.Tests.Integration.TeacherInbox;

public sealed class ReplyToTeacherThreadEndpointTests(ApiFactory factory)
{
    private const string ReplyText = "Because force equals mass times acceleration.";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Reply_ClaimingTeacher_StoresReplyAndStudentSeesNewReply()
    {
        var subjectId = await SeedSubjectAsync();
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var (teacher, client) = await SignedInTeacherForAsync(factory, subjectId);
        var thread = await SeedThreadAsync(factory, new TeacherThreadBuilder().ForStudent(student.Id).WithContext(ContextFor(subjectId)).ClaimedBy(teacher.Id).Build());

        using var response = await client.PostAsJsonAsync($"{Route}/{thread.Id}/replies", new { text = ReplyText }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("status").GetString().Should().Be("Answered");
        var stored = await ReadThreadAsync(factory, thread.Id);
        stored.Messages.Should().HaveCount(2);
        stored.Messages.Single(x => x.SenderId == teacher.Id).Text.Should().Be(ReplyText);
        using var studentClient = await ScopeTestData.SignedInClientAsync(factory, student, CancellationToken);
        var list = await studentClient.GetFromJsonAsync<JsonElement>("/api/teacher-threads", CancellationToken);
        var item = list.GetProperty("items").EnumerateArray().Should().ContainSingle().Subject;
        (item.GetProperty("hasUnreadReply").GetBoolean(), item.GetProperty("status").GetString()).Should().Be((true, "Answered"));
    }

    [Fact]
    public async Task Reply_BlankText_Returns422()
    {
        var subjectId = await SeedSubjectAsync();
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var (teacher, client) = await SignedInTeacherForAsync(factory, subjectId);
        var thread = await SeedThreadAsync(factory, new TeacherThreadBuilder().ForStudent(student.Id).WithContext(ContextFor(subjectId)).ClaimedBy(teacher.Id).Build());

        using var response = await client.PostAsJsonAsync($"{Route}/{thread.Id}/replies", new { text = "   " }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Contain("TEACHER_THREAD_REPLY_TEXT_REQUIRED");
        (await ReadThreadAsync(factory, thread.Id)).Messages.Should().HaveCount(1);
    }

    [Fact]
    public async Task Reply_UnclaimedThread_Returns409NotClaimed()
    {
        var subjectId = await SeedSubjectAsync();
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var (_, client) = await SignedInTeacherForAsync(factory, subjectId);
        var thread = await SeedThreadAsync(factory, new TeacherThreadBuilder().ForStudent(student.Id).WithContext(ContextFor(subjectId)).Build());

        using var response = await client.PostAsJsonAsync($"{Route}/{thread.Id}/replies", new { text = ReplyText }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadCodeAsync(response)).Should().Be("TEACHER_THREAD_NOT_CLAIMED");
    }

    [Fact]
    public async Task Reply_ClaimedByAnother_Returns409AlreadyClaimed()
    {
        var subjectId = await SeedSubjectAsync();
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var (other, _) = await SignedInTeacherForAsync(factory, subjectId);
        var (_, client) = await SignedInTeacherForAsync(factory, subjectId);
        var thread = await SeedThreadAsync(factory, new TeacherThreadBuilder().ForStudent(student.Id).WithContext(ContextFor(subjectId)).ClaimedBy(other.Id).Build());

        using var response = await client.PostAsJsonAsync($"{Route}/{thread.Id}/replies", new { text = ReplyText }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadCodeAsync(response)).Should().Be("TEACHER_THREAD_ALREADY_CLAIMED");
    }

    [Fact]
    public async Task Reply_Student_Returns403()
    {
        var subjectId = await SeedSubjectAsync();
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var thread = await SeedThreadAsync(factory, new TeacherThreadBuilder().ForStudent(student.Id).WithContext(ContextFor(subjectId)).Build());
        using var client = await ScopeTestData.SignedInClientAsync(factory, student, CancellationToken);

        using var response = await client.PostAsJsonAsync($"{Route}/{thread.Id}/replies", new { text = ReplyText }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private Task<Guid> SeedSubjectAsync() => ScopeTestData.SeedSubjectAsync(factory, $"Physics {Guid.NewGuid():N}", CancellationToken);

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response) => (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("code").GetString();
}
