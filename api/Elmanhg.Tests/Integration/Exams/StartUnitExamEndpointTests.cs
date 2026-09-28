using Elmanhg.Domain.Lessons;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Exams.ExamTestData;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Exams;

public sealed class StartUnitExamEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Post_ServableUnit_StartsExamWithBlueprintShape()
    {
        var (_, unitId, _, questionIds) = await SeedExamUnitAsync(factory, 2, timeLimitMinutes: 45, passMark: 60);
        var (_, client) = await SignedInStudentAsync(factory);

        var body = await StartAsync(client, unitId);

        body.GetProperty("kind").GetString().Should().Be("UnitExam");
        ItemQuestionIds(body).Should().BeEquivalentTo(questionIds);
        (body.GetProperty("deadline").GetDateTimeOffset() - body.GetProperty("startedAt").GetDateTimeOffset()).Should().Be(TimeSpan.FromMinutes(45));
        body.GetProperty("items").EnumerateArray().Should().AllSatisfy(x => x.GetProperty("correctAnswer").ValueKind.Should().Be(JsonValueKind.Null));
        var session = await ReadSessionAsync(factory, body.GetProperty("id").GetGuid());
        session.PassMark.Should().Be(60);
        session.Deadline.Should().Be(session.StartedAt.AddMinutes(45));
    }

    [Fact]
    public async Task Post_OpenExamSameUnit_ResumesSameSession()
    {
        var (_, unitId, _, _) = await SeedExamUnitAsync(factory, 2);
        var (student, client) = await SignedInStudentAsync(factory);
        var first = await StartAsync(client, unitId);

        var second = await StartAsync(client, unitId);

        second.GetProperty("id").GetGuid().Should().Be(first.GetProperty("id").GetGuid());
        using var scope = factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Sessions.CountAsync(x => x.StudentId == student.Id, TestContext.Current.CancellationToken)).Should().Be(1);
    }

    [Fact]
    public async Task Post_OpenExamOtherUnit_Returns409()
    {
        var (_, firstUnitId, _, _) = await SeedExamUnitAsync(factory, 1);
        var (_, secondUnitId, _, _) = await SeedExamUnitAsync(factory, 1);
        var (_, client) = await SignedInStudentAsync(factory);
        await StartAsync(client, firstUnitId);

        using var response = await client.PostAsync($"{ExamTestData.Route}/units/{secondUnitId}", null, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadCodeAsync(response)).Should().Be("EXAM_ALREADY_IN_PROGRESS");
    }

    [Fact]
    public async Task Post_NoBlueprint_Returns400()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Physics", 1, cancellationToken);
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, cancellationToken);
        var lessonId = await ContentTestData.SeedLessonInStateAsync(factory, unitId, "Newton's laws", 1, LessonState.Published, cancellationToken);
        await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: true, cancellationToken);
        var (_, client) = await SignedInStudentAsync(factory);

        using var response = await client.PostAsync($"{ExamTestData.Route}/units/{unitId}", null, cancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadCodeAsync(response)).Should().Be("UNIT_EXAM_NO_BLUEPRINT");
    }

    [Fact]
    public async Task Post_Shortfall_Returns400()
    {
        var (_, unitId, _, questionIds) = await SeedExamUnitAsync(factory, 2);
        await RetireQuestionAsync(factory, questionIds[1]);
        var (_, client) = await SignedInStudentAsync(factory);

        using var response = await client.PostAsync($"{ExamTestData.Route}/units/{unitId}", null, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadCodeAsync(response)).Should().Be("EXAM_SHORTFALL");
    }

    [Fact]
    public async Task Post_ExpiredOpenExam_SubmitsAndReturnsIt()
    {
        var (_, unitId, _, _) = await SeedExamUnitAsync(factory, 2);
        var (_, client) = await SignedInStudentAsync(factory);
        var started = await StartAsync(client, unitId);
        var sessionId = started.GetProperty("id").GetGuid();
        using var save = await SaveAsync(client, sessionId, ItemQuestionIds(started)[0], "b");
        await ExpireAsync(factory, sessionId, TimeSpan.FromHours(1));

        var body = await StartAsync(client, unitId);

        body.GetProperty("id").GetGuid().Should().Be(sessionId);
        body.GetProperty("submittedAt").ValueKind.Should().Be(JsonValueKind.String);
        (await ReadAttemptsAsync(factory, sessionId)).Should().ContainSingle().Which.QuestionId.Should().Be(ItemQuestionIds(started)[0]);
    }

    [Fact]
    public async Task Post_Admin_StartsTestModeExam()
    {
        var (_, unitId, _, _) = await SeedExamUnitAsync(factory, 1);
        var admin = await ScopeTestData.SeedAdminAsync(factory, TestContext.Current.CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, admin, TestContext.Current.CancellationToken);

        var body = await StartAsync(client, unitId);

        body.GetProperty("isTestMode").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Post_Teacher_Returns403()
    {
        var (_, unitId, _, _) = await SeedExamUnitAsync(factory, 1);
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, TestContext.Current.CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, TestContext.Current.CancellationToken);

        using var response = await client.PostAsync($"{ExamTestData.Route}/units/{unitId}", null, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_Anonymous_Returns401()
    {
        using var client = AuthTestClient.Create(factory);

        using var response = await client.PostAsync($"{ExamTestData.Route}/units/{Guid.NewGuid()}", null, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
