using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.TeacherInbox.TeacherInboxTestData;

namespace Elmanhg.Tests.Integration.TeacherInbox;

public sealed class TeacherInboxEndpointTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetInbox_AssignedTeacher_ListsOnlyAssignedSubjectThreads()
    {
        var (physics, math, student) = await SeedSubjectsAndStudentAsync();
        var (_, client) = await SignedInTeacherForAsync(factory, physics);
        var physicsThread = await SeedThreadAsync(factory, new TeacherThreadBuilder().ForStudent(student).WithContext(ContextFor(physics)).Build());
        await SeedThreadAsync(factory, new TeacherThreadBuilder().ForStudent(student).WithContext(ContextFor(math)).Build());

        var body = await client.GetFromJsonAsync<JsonElement>(Route, CancellationToken);

        var item = body.GetProperty("items").EnumerateArray().Should().ContainSingle().Subject;
        item.GetProperty("id").GetGuid().Should().Be(physicsThread.Id);
        item.GetProperty("studentName").GetString().Should().Be("Student");
        item.GetProperty("teacherName").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task GetInbox_OpenThreadsFirstByDueTime()
    {
        var (physics, _, student) = await SeedSubjectsAndStudentAsync();
        var (teacher, client) = await SignedInTeacherForAsync(factory, physics);
        var answered = await SeedThreadAsync(factory, new TeacherThreadBuilder().ForStudent(student).WithContext(ContextFor(physics)).SubmittedAt(TeacherThreadBuilder.DefaultSubmittedAt.AddHours(-5)).AnsweredBy(teacher.Id).Build());
        var newer = await SeedThreadAsync(factory, new TeacherThreadBuilder().ForStudent(student).WithContext(ContextFor(physics)).SubmittedAt(TeacherThreadBuilder.DefaultSubmittedAt.AddHours(1)).Build());
        var older = await SeedThreadAsync(factory, new TeacherThreadBuilder().ForStudent(student).WithContext(ContextFor(physics)).Build());

        var body = await client.GetFromJsonAsync<JsonElement>(Route, CancellationToken);

        body.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).Should().Equal(older.Id, newer.Id, answered.Id);
    }

    [Fact]
    public async Task GetInbox_MineFilter_ListsOnlyOwnClaims()
    {
        var (physics, _, student) = await SeedSubjectsAndStudentAsync();
        var (teacher, client) = await SignedInTeacherForAsync(factory, physics);
        var (other, _) = await SignedInTeacherForAsync(factory, physics);
        var mine = await SeedThreadAsync(factory, new TeacherThreadBuilder().ForStudent(student).WithContext(ContextFor(physics)).ClaimedBy(teacher.Id).Build());
        await SeedThreadAsync(factory, new TeacherThreadBuilder().ForStudent(student).WithContext(ContextFor(physics)).ClaimedBy(other.Id).Build());
        await SeedThreadAsync(factory, new TeacherThreadBuilder().ForStudent(student).WithContext(ContextFor(physics)).Build());

        var body = await client.GetFromJsonAsync<JsonElement>($"{Route}?filter=Mine", CancellationToken);

        var item = body.GetProperty("items").EnumerateArray().Should().ContainSingle().Subject;
        (item.GetProperty("id").GetGuid(), item.GetProperty("isClaimedByMe").GetBoolean(), item.GetProperty("teacherName").GetString()).Should().Be((mine.Id, true, "Teacher"));
    }

    [Fact]
    public async Task GetInbox_UnknownFilter_Returns400()
    {
        var (physics, _, _) = await SeedSubjectsAndStudentAsync();
        var (_, client) = await SignedInTeacherForAsync(factory, physics);

        using var response = await client.GetAsync($"{Route}?filter=99", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("errors").TryGetProperty("filter", out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetInbox_Student_Returns403()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, student, CancellationToken);

        using var response = await client.GetAsync(Route, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetInbox_Anonymous_Returns401()
    {
        using var client = AuthTestClient.Create(factory);

        using var response = await client.GetAsync(Route, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetThread_AssignedTeacher_ReturnsContextMessagesAndCanClaim()
    {
        var (physics, _, student) = await SeedSubjectsAndStudentAsync();
        var (_, client) = await SignedInTeacherForAsync(factory, physics);
        var thread = await SeedThreadAsync(factory, new TeacherThreadBuilder().ForStudent(student).WithContext(ContextFor(physics)).Build());

        var body = await client.GetFromJsonAsync<JsonElement>($"{Route}/{thread.Id}", CancellationToken);

        body.GetProperty("context").GetProperty("lessonName").GetString().Should().Be("Newton's laws");
        body.GetProperty("messages")[0].GetProperty("text").GetString().Should().Be("Why is F = ma?");
        (body.GetProperty("canClaim").GetBoolean(), body.GetProperty("canReply").GetBoolean()).Should().Be((true, false));
    }

    [Fact]
    public async Task GetThread_TeacherOfOtherSubject_Returns403()
    {
        var (physics, math, student) = await SeedSubjectsAndStudentAsync();
        var (_, client) = await SignedInTeacherForAsync(factory, math);
        var thread = await SeedThreadAsync(factory, new TeacherThreadBuilder().ForStudent(student).WithContext(ContextFor(physics)).Build());

        using var response = await client.GetAsync($"{Route}/{thread.Id}", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReadCodeAsync(response)).Should().Be("SUBJECT_OUT_OF_SCOPE");
    }

    [Fact]
    public async Task GetThread_Unknown_Returns404()
    {
        var (physics, _, _) = await SeedSubjectsAndStudentAsync();
        var (_, client) = await SignedInTeacherForAsync(factory, physics);

        using var response = await client.GetAsync($"{Route}/{Guid.NewGuid()}", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("TEACHER_THREAD_NOT_FOUND");
    }

    private async Task<(Guid Physics, Guid Math, Guid StudentId)> SeedSubjectsAndStudentAsync()
    {
        var physics = await ScopeTestData.SeedSubjectAsync(factory, $"Physics {Guid.NewGuid():N}", CancellationToken);
        var math = await ScopeTestData.SeedSubjectAsync(factory, $"Math {Guid.NewGuid():N}", CancellationToken);
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        return (physics, math, student.Id);
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response) => (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("code").GetString();
}
