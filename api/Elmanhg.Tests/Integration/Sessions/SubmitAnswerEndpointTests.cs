using Elmanhg.Domain.Questions.Schemas;
using Elmanhg.Domain.Sessions;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Sessions;

public sealed class SubmitAnswerEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Post_CorrectAnswer_Returns200AndPersistsAttempt()
    {
        var (student, client, sessionId, questionId) = await StartAsync();

        using var response = await AnswerAsync(client, sessionId, questionId, "b");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("attempt").GetProperty("outcome").GetString().Should().Be("Correct");
        body.GetProperty("correctAnswer").ValueKind.Should().Be(JsonValueKind.Object);
        var attempt = (await ReadAttemptsAsync(factory, sessionId)).Should().ContainSingle().Subject;
        (attempt.Score, attempt.NormalisedScore, attempt.QuestionVersion).Should().Be((1m, 1m, 1));
        QuestionJson.AreEquivalent(attempt.Answer, SessionBuilder.AnswerB).Should().BeTrue();
        attempt.GradedBy.Should().Be(AttemptGrader.Auto);
        attempt.TimeTakenMilliseconds.Should().BeInRange(0, 1000);
        attempt.StudentId.Should().Be(student);
    }

    [Fact]
    public async Task Post_AfterAnHourIdle_KeepsReportedTimeMeasuresUnreportedAndSumsSession()
    {
        var (lessonId, _) = await SeedServableLessonAsync(factory, 2);
        var (_, client) = await SignedInStudentAsync(factory);
        var started = await StartQuizAsync(client, lessonId);
        var sessionId = started.GetProperty("id").GetGuid();
        var questionIds = started.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("questionId").GetGuid()).ToList();
        await BackdateLastActivityAsync(sessionId);
        using var reported = await AnswerAsync(client, sessionId, questionIds[0], "b", 12000);
        await BackdateLastActivityAsync(sessionId);
        using var unreported = await AnswerAsync(client, sessionId, questionIds[1], "b", null);

        using var response = await client.GetAsync($"{Route}/{sessionId}", TestContext.Current.CancellationToken);

        (reported.StatusCode, unreported.StatusCode).Should().Be((HttpStatusCode.OK, HttpStatusCode.OK));
        var attempts = await ReadAttemptsAsync(factory, sessionId);
        attempts.Single(x => x.QuestionId == questionIds[0]).TimeTakenMilliseconds.Should().Be(12000);
        attempts.Single(x => x.QuestionId == questionIds[1]).TimeTakenMilliseconds.Should().BeGreaterThanOrEqualTo(3_600_000);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("timeTakenMilliseconds").GetInt64().Should().Be(attempts.Sum(x => (long)x.TimeTakenMilliseconds));
    }

    [Fact]
    public async Task Post_SameAnswerTwice_Returns200SameAttemptOneRow()
    {
        var (_, client, sessionId, questionId) = await StartAsync();
        using var first = await AnswerAsync(client, sessionId, questionId, "b");

        using var second = await AnswerAsync(client, sessionId, questionId, "b");

        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadAttemptIdAsync(second)).Should().Be(await ReadAttemptIdAsync(first));
        (await ReadAttemptsAsync(factory, sessionId)).Should().HaveCount(1);
    }

    [Fact]
    public async Task Post_DifferentAnswerAfterAnswering_Returns409()
    {
        var (_, client, sessionId, questionId) = await StartAsync();
        using var first = await AnswerAsync(client, sessionId, questionId, "b");

        using var second = await AnswerAsync(client, sessionId, questionId, "a");

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ReadCodeAsync(second)).Should().Be("SESSION_QUESTION_ALREADY_ANSWERED");
        (await ReadAttemptsAsync(factory, sessionId)).Should().HaveCount(1);
    }

    [Fact]
    public async Task Post_QuestionEditedAfterServing_GradesServedVersion()
    {
        var (_, client, sessionId, questionId) = await StartAsync();
        await EditQuestionContentAsync(factory, questionId, QuestionBuilder.McqContent() with { GradingSpec = """{"correctOptionId":"a"}""" });

        using var response = await AnswerAsync(client, sessionId, questionId, "b");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("attempt").GetProperty("outcome").GetString().Should().Be("Correct");
        (await ReadAttemptsAsync(factory, sessionId)).Single().QuestionVersion.Should().Be(1);
    }

    [Fact]
    public async Task Post_OtherStudentsSession_Returns404SessionNotFound()
    {
        var (_, _, sessionId, questionId) = await StartAsync();
        var (_, other) = await SignedInStudentAsync(factory);

        using var response = await AnswerAsync(other, sessionId, questionId, "b");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("SESSION_NOT_FOUND");
        (await ReadAttemptsAsync(factory, sessionId)).Should().BeEmpty();
    }

    [Fact]
    public async Task Post_QuestionNotInSession_Returns404SessionQuestionNotFound()
    {
        var (_, client, sessionId, _) = await StartAsync();

        using var response = await AnswerAsync(client, sessionId, Guid.NewGuid(), "b");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("SESSION_QUESTION_NOT_FOUND");
    }

    [Fact]
    public async Task Post_AnswerWrongShape_Returns422QuestionAnswerInvalid()
    {
        var (_, client, sessionId, questionId) = await StartAsync();

        using var response = await client.PostAsJsonAsync($"{Route}/{sessionId}/answers", new { questionId, answer = new { optionId = 5 } }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Contain("QUESTION_ANSWER_INVALID");
        (await ReadAttemptsAsync(factory, sessionId)).Should().BeEmpty();
    }

    [Fact]
    public async Task Post_Anonymous_Returns401()
    {
        var (_, _, sessionId, questionId) = await StartAsync();
        using var anonymous = AuthTestClient.Create(factory);

        using var response = await AnswerAsync(anonymous, sessionId, questionId, "b");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<(Guid StudentId, HttpClient Client, Guid SessionId, Guid QuestionId)> StartAsync()
    {
        var (lessonId, _) = await SeedServableLessonAsync(factory, 1).ConfigureAwait(false);
        var (student, client) = await SignedInStudentAsync(factory).ConfigureAwait(false);
        var body = await StartQuizAsync(client, lessonId).ConfigureAwait(false);
        return (student.Id, client, body.GetProperty("id").GetGuid(), body.GetProperty("items")[0].GetProperty("questionId").GetGuid());
    }

    private async Task BackdateLastActivityAsync(Guid sessionId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.ExecuteSqlAsync($"UPDATE \"Sessions\" SET \"LastActivityAt\" = now() - interval '1 hour' WHERE \"Id\" = {sessionId}", TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    private static async Task<Guid> ReadAttemptIdAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false);
        return body.GetProperty("attempt").GetProperty("id").GetGuid();
    }

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken).ConfigureAwait(false);
        return body.GetProperty("code").GetString();
    }
}
