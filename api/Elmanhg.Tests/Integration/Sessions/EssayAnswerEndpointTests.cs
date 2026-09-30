using Elmanhg.Domain.EssayGrading;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.TrainingData;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.EssayGrading;
using Elmanhg.Tests.Integration.Exams;
using Elmanhg.Tests.Integration.Infrastructure;
using Elmanhg.Tests.Integration.Mastery;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Sessions;

public sealed class EssayAnswerEndpointTests(ApiFactory factory)
{
    private const string EssayText = "القصور الذاتي هو ممانعة الجسم لتغيير حالته الحركية.";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PostAnswer_WrittenEssay_Returns200WithPendingAnswerAndPendingGrade()
    {
        var (student, client, sessionId, questionId) = await StartEssayQuizAsync();

        using var response = await EssayAsync(client, sessionId, questionId, $"  {EssayText}  ");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);
        body.GetProperty("attempt").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("pendingAnswer").GetProperty("text").GetString().Should().Be(EssayText);
        var grade = await EssayGradingTestData.ReadGradeForAsync(factory, sessionId, questionId);
        (grade.Status, grade.StudentId, grade.MaxScore).Should().Be((EssayGradeStatus.Pending, student, 5));
        grade.TimeTakenMilliseconds.Should().BeInRange(0, 1000);
        grade.SubjectId.Should().NotBeEmpty();
        (await ReadAttemptsAsync(factory, sessionId)).Should().BeEmpty();
    }

    [Fact]
    public async Task PostAnswer_EssayOver20000Characters_Returns422QuestionEssayAnswerTooLong()
    {
        var (_, client, sessionId, questionId) = await StartEssayQuizAsync();

        using var response = await EssayAsync(client, sessionId, questionId, new string('ب', 20001));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ExamTestData.ReadCodeAsync(response)).Should().Be("QUESTION_ESSAY_ANSWER_TOO_LONG");
    }

    [Fact]
    public async Task PostAnswer_DifferentEssayAfterSubmit_Returns409SessionQuestionAlreadyAnswered()
    {
        var (_, client, sessionId, questionId) = await StartEssayQuizAsync();
        using var first = await EssayAsync(client, sessionId, questionId, EssayText);

        using var response = await EssayAsync(client, sessionId, questionId, "إجابة مختلفة");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ExamTestData.ReadCodeAsync(response)).Should().Be("SESSION_QUESTION_ALREADY_ANSWERED");
    }

    [Fact]
    public async Task GetSession_PendingEssay_ReturnsPendingAnswerAndNoCurrentPosition()
    {
        var (_, client, sessionId, questionId) = await StartEssayQuizAsync();
        using var answered = await EssayAsync(client, sessionId, questionId, EssayText);

        var session = await client.GetFromJsonAsync<JsonElement>($"{Route}/{sessionId}", CancellationToken);

        session.GetProperty("currentPosition").ValueKind.Should().Be(JsonValueKind.Null);
        session.GetProperty("items")[0].GetProperty("pendingAnswer").GetProperty("text").GetString().Should().Be(EssayText);
    }

    [Fact]
    public async Task GradeEssay_AfterQuizFinished_WritesAiAttemptAndUpdatesScore()
    {
        var (student, client, sessionId, questionId) = await StartEssayQuizAsync();
        using var answered = await EssayAsync(client, sessionId, questionId, EssayText);
        using var finished = await FinishAsync(client, sessionId);
        var before = await finished.Content.ReadFromJsonAsync<JsonElement>(CancellationToken);
        var grade = await EssayGradingTestData.ReadGradeForAsync(factory, sessionId, questionId);

        await EssayGradingTestData.GradeAsync(factory, grade.Id);

        var after = await client.GetFromJsonAsync<JsonElement>($"{Route}/{sessionId}", CancellationToken);
        (before.GetProperty("scorePercent").GetDecimal(), after.GetProperty("scorePercent").GetDecimal()).Should().Be((0m, 100m));
        after.GetProperty("items")[0].GetProperty("attempt").GetProperty("score").GetDecimal().Should().Be(5m);
        (await ReadAttemptsAsync(factory, sessionId)).Should().ContainSingle().Which.GradedBy.Should().Be(AttemptGrader.AI);
        (await MasteryTestData.ReadMasteriesAsync(factory, student)).Should().ContainSingle().Which.QuestionId.Should().Be(questionId);
    }

    [Fact]
    public async Task GradeEssay_AppliedTwice_WritesOneAiTrainingRecord()
    {
        var (_, client, sessionId, questionId) = await StartEssayQuizAsync();
        using var answered = await EssayAsync(client, sessionId, questionId, EssayText);
        var grade = await EssayGradingTestData.ReadGradeForAsync(factory, sessionId, questionId);

        await EssayGradingTestData.GradeAsync(factory, grade.Id);
        await EssayGradingTestData.GradeAsync(factory, grade.Id);

        var attempt = (await ReadAttemptsAsync(factory, sessionId)).Should().ContainSingle().Subject;
        using var scope = factory.Services.CreateScope();
        var records = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AttemptTrainingRecords.AsNoTracking().Where(x => x.QuestionId == questionId).ToListAsync(CancellationToken);
        records.Should().ContainSingle().Which.Should().Match<AttemptTrainingRecord>(x => x.AttemptId == attempt.Id && x.GradedBy == AttemptGrader.AI);
    }

    [Fact]
    public async Task PostAnswer_OtherStudentsSession_Returns404SessionNotFound()
    {
        var (_, _, sessionId, questionId) = await StartEssayQuizAsync();
        var (_, other) = await SignedInStudentAsync(factory);

        using var response = await EssayAsync(other, sessionId, questionId, EssayText);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ExamTestData.ReadCodeAsync(response)).Should().Be("SESSION_NOT_FOUND");
    }

    [Fact]
    public async Task PostAnswer_McqOver4000Characters_Returns422AttemptAnswerTooLong()
    {
        var (lessonId, questionIds) = await SeedServableLessonAsync(factory, 1);
        var (_, client) = await SignedInStudentAsync(factory);
        var sessionId = (await StartQuizAsync(client, lessonId)).GetProperty("id").GetGuid();

        using var response = await client.PostAsJsonAsync($"{Route}/{sessionId}/answers", new { questionId = questionIds[0], answer = new { optionId = "b", padding = new string('x', 4000) } }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ExamTestData.ReadCodeAsync(response)).Should().Be("ATTEMPT_ANSWER_TOO_LONG");
    }

    private async Task<(Guid Student, HttpClient Client, Guid SessionId, Guid QuestionId)> StartEssayQuizAsync()
    {
        var (lessonId, questionId) = await EssayGradingTestData.SeedServableEssayLessonAsync(factory);
        var (student, client) = await SignedInStudentAsync(factory);
        var started = await StartQuizAsync(client, lessonId);
        return (student.Id, client, started.GetProperty("id").GetGuid(), questionId);
    }

    private static Task<HttpResponseMessage> EssayAsync(HttpClient client, Guid sessionId, Guid questionId, string text)
    {
        return client.PostAsJsonAsync($"{Route}/{sessionId}/answers", new { questionId, answer = new { text }, timeTakenMilliseconds = 1000 }, CancellationToken);
    }
}
