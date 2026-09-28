using Elmanhg.Domain.Questions.Schemas;
using Elmanhg.Tests.Integration.Auth;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Exams.ExamTestData;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Exams;

public sealed class SaveExamAnswerEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Put_ValidAnswer_SavesWithoutAttempt()
    {
        var (client, sessionId, questionIds) = await StartExamAsync();

        using var response = await SaveAsync(client, sessionId, questionIds[0], "b");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        body.GetProperty("answerSavedAt").ValueKind.Should().Be(JsonValueKind.String);
        var session = await ReadSessionAsync(factory, sessionId);
        QuestionJson.AreEquivalent(session.Items.Single(x => x.QuestionId == questionIds[0]).SavedAnswer ?? string.Empty, "{\"optionId\":\"b\"}").Should().BeTrue();
        (await ReadAttemptsAsync(factory, sessionId)).Should().BeEmpty();
    }

    [Fact]
    public async Task Put_ChangedAnswer_OverwritesSavedAnswer()
    {
        var (client, sessionId, questionIds) = await StartExamAsync();
        using var first = await SaveAsync(client, sessionId, questionIds[0], "b");

        using var second = await SaveAsync(client, sessionId, questionIds[0], "a");

        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var saved = (await ReadSessionAsync(factory, sessionId)).Items.Single(x => x.QuestionId == questionIds[0]).SavedAnswer;
        QuestionJson.AreEquivalent(saved ?? string.Empty, "{\"optionId\":\"a\"}").Should().BeTrue();
    }

    [Fact]
    public async Task Put_AfterDeadline_Returns400()
    {
        var (client, sessionId, questionIds) = await StartExamAsync();
        await ExpireAsync(factory, sessionId, TimeSpan.FromHours(1));

        using var response = await SaveAsync(client, sessionId, questionIds[0], "b");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadCodeAsync(response)).Should().Be("EXAM_TIME_EXPIRED");
    }

    [Fact]
    public async Task Put_SubmittedExam_Returns400()
    {
        var (client, sessionId, questionIds) = await StartExamAsync();
        using var submit = await SubmitAsync(client, sessionId);

        using var response = await SaveAsync(client, sessionId, questionIds[0], "b");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadCodeAsync(response)).Should().Be("SESSION_ALREADY_SUBMITTED");
    }

    [Fact]
    public async Task Put_WrongShape_Returns422()
    {
        var (client, sessionId, questionIds) = await StartExamAsync();

        using var response = await client.PutAsJsonAsync($"{ExamTestData.Route}/{sessionId}/answers/{questionIds[0]}", new { answer = new { optionId = 5 } }, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadCodeAsync(response)).Should().Be("QUESTION_ANSWER_INVALID");
    }

    [Fact]
    public async Task Put_QuestionNotInExam_Returns404()
    {
        var (client, sessionId, _) = await StartExamAsync();

        using var response = await SaveAsync(client, sessionId, Guid.NewGuid(), "b");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("SESSION_QUESTION_NOT_FOUND");
    }

    [Fact]
    public async Task Put_OtherStudent_Returns404()
    {
        var (_, sessionId, questionIds) = await StartExamAsync();
        var (_, other) = await SignedInStudentAsync(factory);

        using var response = await SaveAsync(other, sessionId, questionIds[0], "b");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ReadCodeAsync(response)).Should().Be("SESSION_NOT_FOUND");
        (await ReadSessionAsync(factory, sessionId)).Items.Should().AllSatisfy(x => x.SavedAnswer.Should().BeNull());
    }

    [Fact]
    public async Task Put_Anonymous_Returns401()
    {
        using var client = AuthTestClient.Create(factory);

        using var response = await SaveAsync(client, Guid.NewGuid(), Guid.NewGuid(), "b");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<(HttpClient Client, Guid SessionId, List<Guid> QuestionIds)> StartExamAsync()
    {
        var (_, unitId, _, _) = await SeedExamUnitAsync(factory, 2).ConfigureAwait(false);
        var (_, client) = await SignedInStudentAsync(factory).ConfigureAwait(false);
        var body = await StartAsync(client, unitId).ConfigureAwait(false);
        return (client, body.GetProperty("id").GetGuid(), ItemQuestionIds(body));
    }
}
