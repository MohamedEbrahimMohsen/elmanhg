using Elmanhg.Domain.Lessons;
using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Infrastructure;
using Elmanhg.Tests.Integration.Mastery;
using FluentAssertions;
using System.Net;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Progress.ProgressTestData;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Progress;

public sealed class SubjectProgressEndpointTests(ApiFactory factory)
{
    private const string SubjectsPath = "subjects";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_Anonymous_Returns401()
    {
        using var anonymous = AuthTestClient.Create(factory);

        using var response = await anonymous.GetAsync($"{ProgressTestData.Route}/{SubjectsPath}", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Teacher_Returns403()
    {
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, teacher, CancellationToken);

        using var response = await client.GetAsync($"{ProgressTestData.Route}/{SubjectsPath}", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_Admin_Returns403()
    {
        var admin = await ScopeTestData.SeedAdminAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, admin, CancellationToken);

        using var response = await client.GetAsync($"{ProgressTestData.Route}/{SubjectsPath}", CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_AfterPractice_ReturnsSubjectAndUnitMastery()
    {
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Physics", 1, CancellationToken);
        var mechanicsId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, CancellationToken);
        var wavesId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Waves", 2, CancellationToken);
        var lessonId = await ContentTestData.SeedLessonInStateAsync(factory, mechanicsId, "Forces", 1, LessonState.Published, CancellationToken);
        var q1 = await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: true, CancellationToken);
        var q2 = await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: true, CancellationToken);
        var (_, client) = await SignedInStudentAsync(factory);
        await MasteryTestData.PracticeAsync(client, lessonId, new Dictionary<Guid, string> { [q1] = "b", [q2] = "a" });
        await MasteryTestData.PracticeAsync(client, lessonId, new Dictionary<Guid, string> { [q1] = "b" });

        var subject = FindSubject(await GetJsonAsync(client, SubjectsPath), subjectId);

        AssertCounts(subject, servable: 2, mastered: 1, seen: 2, percent: 50);
        var units = subject.GetProperty("units").EnumerateArray().ToList();
        units.Select(x => x.GetProperty("unitId").GetGuid()).Should().Equal(mechanicsId, wavesId);
        AssertCounts(units[0], servable: 2, mastered: 1, seen: 2, percent: 50);
        units[0].GetProperty("bestExamScorePercent").ValueKind.Should().Be(JsonValueKind.Null);
        AssertCounts(units[1], servable: 0, mastered: 0, seen: 0, percent: 0);
    }

    [Fact]
    public async Task Get_UnitExamSessions_ReturnsBestSubmittedNonTestScore()
    {
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Physics", 1, CancellationToken);
        var mechanicsId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, CancellationToken);
        var (student, client) = await SignedInStudentAsync(factory);
        var other = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var startedAt = new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);
        await InsertUnitExamSessionAsync(factory, student.Id, mechanicsId, 60.5m, submitted: true, isTestMode: false, startedAt);
        await InsertUnitExamSessionAsync(factory, student.Id, mechanicsId, 80m, submitted: true, isTestMode: false, startedAt.AddDays(1));
        await InsertUnitExamSessionAsync(factory, student.Id, mechanicsId, 95m, submitted: false, isTestMode: false, startedAt.AddDays(2));
        await InsertUnitExamSessionAsync(factory, student.Id, mechanicsId, 99m, submitted: true, isTestMode: true, startedAt.AddDays(3));
        await InsertUnitExamSessionAsync(factory, other.Id, mechanicsId, 100m, submitted: true, isTestMode: false, startedAt);

        var subject = FindSubject(await GetJsonAsync(client, SubjectsPath), subjectId);

        subject.GetProperty("units").EnumerateArray().Single().GetProperty("bestExamScorePercent").GetDecimal().Should().Be(80.0m);
    }

    private static JsonElement FindSubject(JsonElement body, Guid subjectId) => body.EnumerateArray().Single(x => x.GetProperty("subjectId").GetGuid() == subjectId);

    private static void AssertCounts(JsonElement element, int servable, int mastered, int seen, int percent)
    {
        (element.GetProperty("servableCount").GetInt32(), element.GetProperty("masteredCount").GetInt32(), element.GetProperty("seenCount").GetInt32(), element.GetProperty("masteryPercent").GetInt32()).Should().Be((servable, mastered, seen, percent));
    }
}
