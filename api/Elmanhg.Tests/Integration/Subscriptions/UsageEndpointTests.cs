using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using Elmanhg.Tests.Integration.TeacherThreads;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Subscriptions;

public sealed class UsageEndpointTests(ApiFactory factory)
{
    private const string UsageRoute = $"{SubscriptionTestData.SubscriptionsRoute}/usage";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_FreeStudentAfterTwoAnswers_ReturnsUsedAndRemaining()
    {
        var (lessonId, _) = await SeedServableLessonAsync(factory, 5);
        var (_, client) = await SignedInFreeStudentAsync(factory);
        var session = await StartQuizAsync(client, lessonId, 5);
        var sessionId = session.GetProperty("id").GetGuid();
        foreach (var item in session.GetProperty("items").EnumerateArray().Take(2))
        {
            using var answer = await AnswerAsync(client, sessionId, item.GetProperty("questionId").GetGuid(), "b");
            answer.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var body = await client.GetFromJsonAsync<JsonElement>(UsageRoute, CancellationToken);

        (body.GetProperty("tier").GetString(), body.GetProperty("dailyQuizQuestionLimit").GetInt32(), body.GetProperty("quizQuestionsUsedToday").GetInt32(), body.GetProperty("quizQuestionsRemainingToday").GetInt32()).Should().Be(("Free", 10, 2, 8));
    }

    [Fact]
    public async Task Get_SubscribedStudent_ReturnsUnlimited()
    {
        var (_, client) = await SignedInStudentAsync(factory);

        var body = await client.GetFromJsonAsync<JsonElement>(UsageRoute, CancellationToken);

        body.GetProperty("tier").GetString().Should().Be("Base");
        (body.GetProperty("dailyQuizQuestionLimit").ValueKind, body.GetProperty("quizQuestionsRemainingToday").ValueKind).Should().Be((JsonValueKind.Null, JsonValueKind.Null));
    }

    [Fact]
    public async Task Get_AskTeacherStudentWithThreadsThisMonth_ReturnsMonthlyQuota()
    {
        var (subjectId, _) = await TeacherThreadTestData.SeedPublishedLessonAsync(factory);
        var (student, client) = await TeacherThreadTestData.SignedInAskTeacherStudentAsync(factory);
        await TeacherThreadTestData.SeedThreadsAsync(factory, student.Id, subjectId, 2, DateTimeOffset.UtcNow);

        var body = await client.GetFromJsonAsync<JsonElement>(UsageRoute, CancellationToken);

        (body.GetProperty("monthlyAskTeacherQuestionLimit").GetInt32(), body.GetProperty("askTeacherQuestionsUsedThisMonth").GetInt32(), body.GetProperty("askTeacherQuestionsRemainingThisMonth").GetInt32()).Should().Be((20, 2, 18));
    }

    [Fact]
    public async Task Get_Anonymous_Returns401()
    {
        using var anonymous = AuthTestClient.Create(factory);

        using var response = await anonymous.GetAsync(UsageRoute, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_Admin_Returns403()
    {
        var admin = await ScopeTestData.SeedAdminAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, admin, CancellationToken);

        using var response = await client.GetAsync(UsageRoute, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
