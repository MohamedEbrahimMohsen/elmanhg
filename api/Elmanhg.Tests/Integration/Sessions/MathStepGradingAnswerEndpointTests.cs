using Elmanhg.Domain.MathStepGrading;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Tests.Integration.Infrastructure;
using Elmanhg.Tests.Integration.MathStepGrading;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Sessions;

public sealed class MathStepGradingAnswerEndpointTests(ApiFactory factory)
{
    private static readonly object Answer = new { steps = new[] { " 2x = 4 " }, finalAnswer = "x=2" };

    [Fact]
    public async Task Post_StepGradedAnswer_ReturnsPendingAndStoresMathStepGrade()
    {
        var (client, sessionId, questionId) = await StartStepGradedQuizAsync();

        using var response = await PostAsync(client, sessionId, questionId, Answer);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("attempt").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("pendingAnswer").GetProperty("finalAnswer").GetString().Should().Be("x=2");
        var grade = (await MathStepGradingTestData.ReadGradesForAsync(factory, sessionId)).Should().ContainSingle().Subject;
        (grade.Status, grade.FinalAnswerVerdict, grade.QuestionId, grade.MaxScore).Should().Be((MathStepGradeStatus.Pending, (MathAnswerVerdict?)MathAnswerVerdict.Equivalent, questionId, 2));
        (await ReadAttemptsAsync(factory, sessionId)).Should().BeEmpty();
    }

    [Fact]
    public async Task Post_StepGradedAnswerReplay_ReturnsSamePendingAnswer()
    {
        var (client, sessionId, questionId) = await StartStepGradedQuizAsync();
        using var first = await PostAsync(client, sessionId, questionId, Answer);

        using var response = await PostAsync(client, sessionId, questionId, Answer);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("pendingAnswer").GetProperty("steps")[0].GetString().Should().Be("2x = 4");
        (await MathStepGradingTestData.ReadGradesForAsync(factory, sessionId)).Should().ContainSingle();
    }

    [Fact]
    public async Task Post_DifferentAnswerAfterPending_Returns409()
    {
        var (client, sessionId, questionId) = await StartStepGradedQuizAsync();
        using var first = await PostAsync(client, sessionId, questionId, Answer);

        using var response = await PostAsync(client, sessionId, questionId, new { steps = new[] { "2x = 6" }, finalAnswer = "x=3" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("code").GetString().Should().Be("SESSION_QUESTION_ALREADY_ANSWERED");
    }

    private async Task<(HttpClient Client, Guid SessionId, Guid QuestionId)> StartStepGradedQuizAsync()
    {
        var (lessonId, _) = await SeedServableLessonAsync(factory, 4);
        var mathId = await MathStepGradingTestData.SeedApprovedStepGradedAsync(factory, lessonId);
        var (_, client) = await SignedInStudentAsync(factory);
        var session = await StartQuizAsync(client, lessonId, 5);
        return (client, session.GetProperty("id").GetGuid(), mathId);
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, Guid sessionId, Guid questionId, object answer)
    {
        return client.PostAsJsonAsync($"{Route}/{sessionId}/answers", new { questionId, answer, timeTakenMilliseconds = 1000 }, TestContext.Current.CancellationToken);
    }
}
