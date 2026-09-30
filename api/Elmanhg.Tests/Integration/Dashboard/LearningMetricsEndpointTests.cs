using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using static Elmanhg.Tests.Integration.Dashboard.DashboardTestData;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Dashboard;

public sealed class LearningMetricsEndpointTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_SuccessRate_SubjectFilter_CountsCorrectOverAttempts()
    {
        var (lessonId, subjectId) = await SeedAnsweredLessonAsync();
        using var client = await AdminClientAsync(factory);

        var body = await GetJsonAsync(client, $"success-rate?subjectId={subjectId}");

        body.GetProperty("attempts").GetInt32().Should().Be(2);
        body.GetProperty("correct").GetInt32().Should().Be(1);
        body.GetProperty("rate").GetDecimal().Should().Be(0.5m);
        body.GetProperty("byLesson").EnumerateArray().Should().ContainSingle().Which.GetProperty("id").GetGuid().Should().Be(lessonId);
    }

    [Fact]
    public async Task Get_SuccessRate_AdminTestModeAttempts_AreExcluded()
    {
        var (lessonId, subjectId) = await SeedAnsweredLessonAsync();
        var admin = await ScopeTestData.SeedAdminAsync(factory, CancellationToken);
        using var adminQuizClient = await ScopeTestData.SignedInClientAsync(factory, admin, CancellationToken);
        await AnswerAllAsync(adminQuizClient, lessonId, "b");
        using var client = await AdminClientAsync(factory);

        var body = await GetJsonAsync(client, $"success-rate?subjectId={subjectId}");

        body.GetProperty("attempts").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Get_SolveRate_SubjectFilter_CountsTodaysAttempts()
    {
        var (_, subjectId) = await SeedAnsweredLessonAsync();
        using var client = await AdminClientAsync(factory);

        var body = await GetJsonAsync(client, $"solve-rate?subjectId={subjectId}");

        body.GetProperty("attempts").GetInt64().Should().Be(2);
        body.GetProperty("daily").EnumerateArray().Last().GetProperty("attempts").GetInt64().Should().Be(2);
        body.GetProperty("activeStudentDays").GetInt64().Should().BeGreaterThanOrEqualTo(1);
    }

    private async Task<(Guid LessonId, Guid SubjectId)> SeedAnsweredLessonAsync()
    {
        var (lessonId, _) = await SeedServableLessonAsync(factory, 2);
        var (_, studentClient) = await SignedInStudentAsync(factory);
        await AnswerAllAsync(studentClient, lessonId, "b", "a");
        return (lessonId, await SubjectOfLessonAsync(factory, lessonId));
    }

    private static async Task AnswerAllAsync(HttpClient client, Guid lessonId, params string[] optionIds)
    {
        var started = await StartQuizAsync(client, lessonId);
        var sessionId = started.GetProperty("id").GetGuid();
        var questionIds = started.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("questionId").GetGuid()).ToList();
        for (var index = 0; index < questionIds.Count; index++)
        {
            using var response = await AnswerAsync(client, sessionId, questionIds[index], optionIds[Math.Min(index, optionIds.Length - 1)]);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }
}
