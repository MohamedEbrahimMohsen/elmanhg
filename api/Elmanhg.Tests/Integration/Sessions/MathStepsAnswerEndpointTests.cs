using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Sessions;

public sealed class MathStepsAnswerEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Post_EquivalentFinalAnswer_RecordsCorrectAttempt()
    {
        var (client, sessionId, questionId) = await StartMathQuizAsync();

        using var response = await PostAsync(client, sessionId, questionId, new { steps = new[] { "2x = 4" }, finalAnswer = "x=2" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var attempt = (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("attempt");
        attempt.GetProperty("outcome").GetString().Should().Be("Correct");
        attempt.GetProperty("answer").GetProperty("steps").EnumerateArray().Select(x => x.GetString()).Should().Equal("2x = 4");
        (await ReadAttemptsAsync(factory, sessionId)).Should().ContainSingle().Which.Score.Should().Be(2m);
    }

    [Fact]
    public async Task Post_TooManySteps_Returns422AttemptAnswerTooLong()
    {
        var (client, sessionId, questionId) = await StartMathQuizAsync();

        using var response = await PostAsync(client, sessionId, questionId, new { steps = Enumerable.Repeat("x", 21).ToArray(), finalAnswer = "x=2" });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Be("ATTEMPT_ANSWER_TOO_LONG");
        (await ReadAttemptsAsync(factory, sessionId)).Should().BeEmpty();
    }

    [Fact]
    public async Task Post_FinalAnswerNotString_Returns422QuestionAnswerInvalid()
    {
        var (client, sessionId, questionId) = await StartMathQuizAsync();

        using var response = await PostAsync(client, sessionId, questionId, new { finalAnswer = 5 });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Be("QUESTION_ANSWER_INVALID");
        (await ReadAttemptsAsync(factory, sessionId)).Should().BeEmpty();
    }

    private async Task<(HttpClient Client, Guid SessionId, Guid QuestionId)> StartMathQuizAsync()
    {
        var (lessonId, _) = await SeedServableLessonAsync(factory, 4);
        var mathId = await QuestionTestData.SeedMathStepsQuestionAsync(factory, lessonId, TestContext.Current.CancellationToken);
        var (_, client) = await SignedInStudentAsync(factory);
        var session = await StartQuizAsync(client, lessonId, 5);
        var item = session.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("type").GetString() == "MathSteps");
        item.GetProperty("questionId").GetGuid().Should().Be(mathId);
        return (client, session.GetProperty("id").GetGuid(), mathId);
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, Guid sessionId, Guid questionId, object answer)
    {
        return client.PostAsJsonAsync($"{Route}/{sessionId}/answers", new { questionId, answer, timeTakenMilliseconds = 1000 }, TestContext.Current.CancellationToken);
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false);
        return body.GetProperty("code").GetString();
    }
}
