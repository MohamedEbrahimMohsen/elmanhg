using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Exams.ExamAttemptsTestData;
using static Elmanhg.Tests.Integration.Exams.ExamTestData;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Exams;

public sealed class ExamAttemptsEndpointTests(ApiFactory factory)
{
    private static readonly DateTimeOffset Day1 = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetSessionAttempts_Anonymous_Returns401()
    {
        using var client = AuthTestClient.Create(factory);

        using var response = await client.GetAsync($"{ExamTestData.Route}/{Guid.NewGuid()}/attempts", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetSessionAttempts_Teacher_Returns403()
    {
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, CancellationToken);

        using var response = await client.GetAsync($"{ExamTestData.Route}/{Guid.NewGuid()}/attempts", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetSessionAttempts_OtherStudentsSession_Returns404()
    {
        var (owner, _) = await SignedInStudentAsync(factory);
        var sittingId = await InsertUnitSittingAsync(factory, owner.Id, Guid.NewGuid(), 80m, startedAt: Day1);
        var (_, other) = await SignedInStudentAsync(factory);

        using var response = await other.GetAsync($"{ExamTestData.Route}/{sittingId}/attempts", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("SESSION_NOT_FOUND");
    }

    [Fact]
    public async Task GetSessionAttempts_UnitScope_ListsCountedSittingsNewestFirstWithBest()
    {
        var (unitId, otherUnitId) = (Guid.NewGuid(), Guid.NewGuid());
        var (student, client) = await SignedInStudentAsync(factory);
        var (otherStudent, _) = await SignedInStudentAsync(factory);
        var day1 = await InsertUnitSittingAsync(factory, student.Id, unitId, 60.5m, startedAt: Day1);
        var day2 = await InsertUnitSittingAsync(factory, student.Id, unitId, 80m, startedAt: Day1.AddDays(1));
        var day3 = await InsertUnitSittingAsync(factory, student.Id, unitId, 70m, startedAt: Day1.AddDays(2));
        await InsertUnitSittingAsync(factory, student.Id, unitId, 99m, isTestMode: true, startedAt: Day1.AddDays(3));
        await InsertUnitSittingAsync(factory, student.Id, unitId, null, submitted: false, startedAt: Day1.AddDays(4));
        await InsertMultiSittingAsync(factory, student.Id, Guid.NewGuid(), [unitId, otherUnitId], 20, 90m, Day1);
        await InsertUnitSittingAsync(factory, otherStudent.Id, unitId, 100m, startedAt: Day1);
        await InsertUnitSittingAsync(factory, student.Id, otherUnitId, 100m, startedAt: Day1);

        var body = await GetAttemptsAsync(client, $"{day1}/attempts");

        var attempts = body.GetProperty("attempts").EnumerateArray().ToList();
        attempts.Select(x => x.GetProperty("sessionId").GetGuid()).Should().Equal(day3, day2, day1);
        attempts.Select(x => x.GetProperty("scorePercent").GetDecimal()).Should().Equal(70m, 80m, 60.5m);
        attempts.Select(x => x.GetProperty("isBest").GetBoolean()).Should().Equal(false, true, false);
        body.GetProperty("bestScorePercent").GetDecimal().Should().Be(80m);
    }

    [Fact]
    public async Task GetSessionAttempts_MultiUnitScope_ListsSameUnitsAndSizeOnly()
    {
        var (subjectId, unitA, unitB) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var (student, client) = await SignedInStudentAsync(factory);
        var day1 = await InsertMultiSittingAsync(factory, student.Id, subjectId, [unitA, unitB], 20, 50m, Day1);
        var day2 = await InsertMultiSittingAsync(factory, student.Id, subjectId, [unitB, unitA], 20, 70m, Day1.AddDays(1));
        await InsertMultiSittingAsync(factory, student.Id, subjectId, [unitA, unitB], 40, 90m, Day1.AddDays(2));
        await InsertUnitSittingAsync(factory, student.Id, unitA, 95m, startedAt: Day1.AddDays(3));

        var body = await GetAttemptsAsync(client, $"{day1}/attempts");

        body.GetProperty("attempts").EnumerateArray().Select(x => x.GetProperty("sessionId").GetGuid()).Should().Equal(day2, day1);
        body.GetProperty("bestScorePercent").GetDecimal().Should().Be(70m);
    }

    [Fact]
    public async Task GetUnitAttempts_Student_ReturnsOnlyUnitExamSittings()
    {
        var unitId = await SeedUnitAsync();
        var (student, client) = await SignedInStudentAsync(factory);
        await InsertUnitSittingAsync(factory, student.Id, unitId, 40m, startedAt: Day1);
        await InsertUnitSittingAsync(factory, student.Id, unitId, 65m, startedAt: Day1.AddDays(1));
        await InsertMultiSittingAsync(factory, student.Id, Guid.NewGuid(), [unitId, Guid.NewGuid()], 20, 99m, Day1.AddDays(2));

        var body = await GetAttemptsAsync(client, $"units/{unitId}/attempts");

        body.GetProperty("attempts").GetArrayLength().Should().Be(2);
        body.GetProperty("bestScorePercent").GetDecimal().Should().Be(65m);
    }

    [Fact]
    public async Task GetUnitAttempts_NoSittings_ReturnsEmptyListAndNullBest()
    {
        var unitId = await SeedUnitAsync();
        var (_, client) = await SignedInStudentAsync(factory);

        var body = await GetAttemptsAsync(client, $"units/{unitId}/attempts");

        body.GetProperty("attempts").GetArrayLength().Should().Be(0);
        body.GetProperty("bestScorePercent").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task GetUnitAttempts_UnknownUnit_Returns404()
    {
        var (_, client) = await SignedInStudentAsync(factory);

        using var response = await client.GetAsync($"{ExamTestData.Route}/units/{Guid.NewGuid()}/attempts", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("UNIT_NOT_FOUND");
    }

    [Fact]
    public async Task Retake_StartAfterSubmit_OpensNewSittingAndListsBoth()
    {
        var (_, unitId, _, _) = await SeedExamUnitAsync(factory, 2);
        var (_, client) = await SignedInStudentAsync(factory);
        var first = (await StartAsync(client, unitId)).GetProperty("id").GetGuid();
        await SubmitOkAsync(client, first);
        var retake = await StartAsync(client, unitId);
        var second = retake.GetProperty("id").GetGuid();
        foreach (var questionId in ItemQuestionIds(retake))
        {
            using var saved = await SaveAsync(client, second, questionId, "b");
            saved.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        await SubmitOkAsync(client, second);

        var byUnit = await GetAttemptsAsync(client, $"units/{unitId}/attempts");
        var bySession = await GetAttemptsAsync(client, $"{first}/attempts");

        second.Should().NotBe(first);
        var attempts = byUnit.GetProperty("attempts").EnumerateArray().ToList();
        attempts.Select(x => x.GetProperty("sessionId").GetGuid()).Should().BeEquivalentTo([second, first]);
        var best = attempts.Single(x => x.GetProperty("sessionId").GetGuid() == second);
        (best.GetProperty("scorePercent").GetDecimal(), best.GetProperty("isBest").GetBoolean()).Should().Be((100m, true));
        byUnit.GetProperty("bestScorePercent").GetDecimal().Should().Be(100m);
        bySession.GetProperty("attempts").EnumerateArray().Select(x => x.GetProperty("sessionId").GetGuid()).Should().BeEquivalentTo([second, first]);
    }

    [Fact]
    public async Task GetSessionAttempts_AdminTestModeSitting_ReturnsEmpty()
    {
        var (_, unitId, _, _) = await SeedExamUnitAsync(factory, 1);
        var admin = await ScopeTestData.SeedAdminAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, admin, CancellationToken);
        var sessionId = (await StartAsync(client, unitId)).GetProperty("id").GetGuid();
        await SubmitOkAsync(client, sessionId);

        var body = await GetAttemptsAsync(client, $"{sessionId}/attempts");

        body.GetProperty("attempts").GetArrayLength().Should().Be(0);
        body.GetProperty("bestScorePercent").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task GetUnitAttempts_Anonymous_Returns401()
    {
        using var client = AuthTestClient.Create(factory);

        using var response = await client.GetAsync($"{ExamTestData.Route}/units/{Guid.NewGuid()}/attempts", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task SubmitOkAsync(HttpClient client, Guid sessionId)
    {
        using var submitted = await SubmitAsync(client, sessionId).ConfigureAwait(false);
        submitted.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<Guid> SeedUnitAsync()
    {
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Physics", 1, CancellationToken).ConfigureAwait(false);
        return await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, CancellationToken).ConfigureAwait(false);
    }
}
