using Elmanhg.Domain.TeacherThreads;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.TeacherInbox.TeacherInboxTestData;

namespace Elmanhg.Tests.Integration.TeacherThreads;

public sealed class FollowUpTeacherThreadEndpointTests(ApiFactory factory)
{
    private const string FollowUpText = "Can you show the units?";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task FollowUp_AnsweredThread_ReopensWithNewDeadlineAndKeepsQuota()
    {
        var (student, client) = await TeacherThreadTestData.SignedInAskTeacherStudentAsync(factory);
        var thread = await SeedAsync(student.Id, builder => builder.AnsweredBy);
        var usedBefore = await UsedThisMonthAsync(client);

        using var response = await client.PostAsJsonAsync(FollowUpRoute(thread.Id), new { text = FollowUpText }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);
        (body.GetProperty("status").GetString(), body.GetProperty("canFollowUp").GetBoolean()).Should().Be(("Open", false));
        var stored = await ReadThreadAsync(factory, thread.Id);
        stored.Messages.Should().HaveCount(3);
        stored.SlaDueAt.Should().BeAfter(thread.SlaDueAt);
        (await UsedThisMonthAsync(client)).Should().Be(usedBefore);
    }

    [Fact]
    public async Task FollowUp_OpenThread_Returns409()
    {
        var (student, client) = await TeacherThreadTestData.SignedInAskTeacherStudentAsync(factory);
        var thread = await SeedAsync(student.Id, builder => builder.ClaimedBy);

        using var response = await client.PostAsJsonAsync(FollowUpRoute(thread.Id), new { text = FollowUpText }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadCodeAsync(response)).Should().Be("TEACHER_THREAD_FOLLOW_UP_NOT_ALLOWED");
        var stored = await ReadThreadAsync(factory, thread.Id);
        (stored.Status, stored.Messages.Count, stored.SlaDueAt).Should().Be((TeacherThreadStatus.Open, 1, thread.SlaDueAt));
    }

    [Fact]
    public async Task FollowUp_OtherStudentsThread_Returns404()
    {
        var owner = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var thread = await SeedAsync(owner.Id, builder => builder.AnsweredBy);
        var (_, client) = await TeacherThreadTestData.SignedInAskTeacherStudentAsync(factory);

        using var response = await client.PostAsJsonAsync(FollowUpRoute(thread.Id), new { text = FollowUpText }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("TEACHER_THREAD_NOT_FOUND");
    }

    [Fact]
    public async Task FollowUp_BlankText_Returns422()
    {
        var (student, client) = await TeacherThreadTestData.SignedInAskTeacherStudentAsync(factory);
        var thread = await SeedAsync(student.Id, builder => builder.AnsweredBy);

        using var response = await client.PostAsJsonAsync(FollowUpRoute(thread.Id), new { text = "   " }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Contain("TEACHER_THREAD_TEXT_REQUIRED");
        (await ReadThreadAsync(factory, thread.Id)).Messages.Should().HaveCount(2);
    }

    [Fact]
    public async Task FollowUp_Teacher_Returns403()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var thread = await SeedAsync(student.Id, builder => builder.AnsweredBy);
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, CancellationToken);

        using var response = await client.PostAsJsonAsync(FollowUpRoute(thread.Id), new { text = FollowUpText }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task FollowUp_Anonymous_Returns401()
    {
        using var anonymous = AuthTestClient.Create(factory);

        using var response = await anonymous.PostAsJsonAsync(FollowUpRoute(Guid.NewGuid()), new { text = FollowUpText }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<TeacherThread> SeedAsync(Guid studentId, Func<TeacherThreadBuilder, Func<Guid, TeacherThreadBuilder>> state)
    {
        var subjectId = await ScopeTestData.SeedSubjectAsync(factory, $"Physics {Guid.NewGuid():N}", CancellationToken);
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        var builder = new TeacherThreadBuilder().ForStudent(studentId).WithContext(ContextFor(subjectId)).SubmittedAt(DateTimeOffset.UtcNow.AddHours(-3));
        return await SeedThreadAsync(factory, state(builder)(teacher.Id).Build());
    }

    private static async Task<int> UsedThisMonthAsync(HttpClient client) => (await client.GetFromJsonAsync<JsonElement>("/api/subscriptions/usage", CancellationToken)).GetProperty("askTeacherQuestionsUsedThisMonth").GetInt32();

    private static string FollowUpRoute(Guid threadId) => $"{TeacherThreadTestData.Route}/{threadId}/follow-ups";

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response) => (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken)).GetProperty("code").GetString();
}
