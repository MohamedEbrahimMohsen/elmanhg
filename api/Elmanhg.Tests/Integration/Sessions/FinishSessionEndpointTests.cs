using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Sessions;

public sealed class FinishSessionEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Post_AfterOneOfTwoAnswers_Returns200WithScoreAndPersists()
    {
        var (_, client, sessionId, questionIds) = await StartAsync(2);
        using var answer = await AnswerAsync(client, sessionId, questionIds[0], "b");

        using var response = await FinishAsync(client, sessionId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("scorePercent").GetDecimal().Should().Be(50m);
        body.GetProperty("submittedAt").ValueKind.Should().Be(JsonValueKind.String);
        var session = await ReadSessionAsync(factory, sessionId);
        session.SubmittedAt.Should().NotBeNull();
        session.ScorePercent.Should().Be(50m);
    }

    [Fact]
    public async Task Post_Twice_Returns200SameSubmittedAt()
    {
        var (_, client, sessionId, _) = await StartAsync(1);
        using var first = await FinishAsync(client, sessionId);

        using var second = await FinishAsync(client, sessionId);

        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadSubmittedAtAsync(second)).Should().Be(await ReadSubmittedAtAsync(first));
    }

    [Fact]
    public async Task Post_AnswerAfterFinish_Returns400SessionAlreadySubmitted()
    {
        var (_, client, sessionId, questionIds) = await StartAsync(1);
        using var finish = await FinishAsync(client, sessionId);

        using var response = await AnswerAsync(client, sessionId, questionIds[0], "b");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadCodeAsync(response)).Should().Be("SESSION_ALREADY_SUBMITTED");
        (await ReadAttemptsAsync(factory, sessionId)).Should().BeEmpty();
    }

    [Fact]
    public async Task Post_StartAfterFinish_StartsNewSession()
    {
        var (lessonId, client, sessionId, _) = await StartAsync(1);
        using var finish = await FinishAsync(client, sessionId);

        var next = await StartQuizAsync(client, lessonId);

        next.GetProperty("id").GetGuid().Should().NotBe(sessionId);
    }

    [Fact]
    public async Task Post_OtherStudentsSession_Returns404()
    {
        var (_, _, sessionId, _) = await StartAsync(1);
        var (_, other) = await SignedInStudentAsync(factory);

        using var response = await FinishAsync(other, sessionId);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("SESSION_NOT_FOUND");
        (await ReadSessionAsync(factory, sessionId)).SubmittedAt.Should().BeNull();
    }

    private async Task<(Guid LessonId, HttpClient Client, Guid SessionId, List<Guid> QuestionIds)> StartAsync(int questionCount)
    {
        var (lessonId, _) = await SeedServableLessonAsync(factory, questionCount).ConfigureAwait(false);
        var (_, client) = await SignedInStudentAsync(factory).ConfigureAwait(false);
        var body = await StartQuizAsync(client, lessonId).ConfigureAwait(false);
        var questionIds = body.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("questionId").GetGuid()).ToList();
        return (lessonId, client, body.GetProperty("id").GetGuid(), questionIds);
    }

    private static Task<HttpResponseMessage> FinishAsync(HttpClient client, Guid sessionId) => client.PostAsync($"{Route}/{sessionId}/finish", null, TestContext.Current.CancellationToken);

    private static async Task<DateTimeOffset> ReadSubmittedAtAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false);
        return body.GetProperty("submittedAt").GetDateTimeOffset();
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false);
        return body.GetProperty("code").GetString();
    }
}
