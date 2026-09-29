using Elmanhg.Domain.Lessons;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Exams;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;

namespace Elmanhg.Tests.Integration.Subscriptions;

public sealed class FreeTierQuizEndpointTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PostAnswer_FreeStudentEleventhQuestion_Returns403AndStoresNoAttempt()
    {
        var (lessonId, _) = await SeedServableLessonAsync(factory, 11);
        var (_, client) = await SignedInFreeStudentAsync(factory);
        var session = await StartQuizAsync(client, lessonId, 20);
        var sessionId = session.GetProperty("id").GetGuid();
        var questionIds = QuestionIds(session);
        await AnswerAllOkAsync(client, sessionId, questionIds.Take(10));

        using var response = await AnswerAsync(client, sessionId, questionIds[10], "b");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ExamTestData.ReadCodeAsync(response)).Should().Be("QUIZ_DAILY_LIMIT_REACHED");
        (await ReadAttemptsAsync(factory, sessionId)).Should().HaveCount(10);
    }

    [Fact]
    public async Task PostQuiz_FreeStudentAtLimitAfterFinishing_Returns403()
    {
        var (lessonId, _) = await SeedServableLessonAsync(factory, 10);
        var (_, client) = await SignedInFreeStudentAsync(factory);
        var session = await StartQuizAsync(client, lessonId, 10);
        var sessionId = session.GetProperty("id").GetGuid();
        await AnswerAllOkAsync(client, sessionId, QuestionIds(session));
        using var finish = await FinishAsync(client, sessionId);
        finish.EnsureSuccessStatusCode();

        using var response = await client.PostAsJsonAsync($"{Route}/quiz", new { lessonId, questionCount = 10 }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ExamTestData.ReadCodeAsync(response)).Should().Be("QUIZ_DAILY_LIMIT_REACHED");
    }

    [Fact]
    public async Task PostQuiz_FreeStudentSecondLessonOfUnit_Returns403LessonLocked()
    {
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Physics", 1, CancellationToken);
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, CancellationToken);
        await ContentTestData.SeedLessonInStateAsync(factory, unitId, "Forces", 1, LessonState.Published, CancellationToken);
        var second = await ContentTestData.SeedLessonInStateAsync(factory, unitId, "Energy", 2, LessonState.Published, CancellationToken);
        await QuestionTestData.SeedQuestionAsync(factory, second, approved: true, CancellationToken);
        var (student, client) = await SignedInFreeStudentAsync(factory);

        using var response = await client.PostAsJsonAsync($"{Route}/quiz", new { lessonId = second }, CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ExamTestData.ReadCodeAsync(response)).Should().Be("LESSON_LOCKED");
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await context.Sessions.AnyAsync(x => x.StudentId == student.Id, CancellationToken)).Should().BeFalse();
    }

    [Fact]
    public async Task PostAnswer_SubscribedStudentEleventhQuestion_Returns200()
    {
        var (lessonId, _) = await SeedServableLessonAsync(factory, 11);
        var (_, client) = await SignedInStudentAsync(factory);
        var session = await StartQuizAsync(client, lessonId, 20);

        await AnswerAllOkAsync(client, session.GetProperty("id").GetGuid(), QuestionIds(session));

        (await ReadAttemptsAsync(factory, session.GetProperty("id").GetGuid())).Should().HaveCount(11);
    }

    [Fact]
    public async Task PostAnswer_AdminTestModeEleventhQuestion_Returns200()
    {
        var (lessonId, _) = await SeedServableLessonAsync(factory, 11);
        var admin = await ScopeTestData.SeedAdminAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, admin, CancellationToken);
        var session = await StartQuizAsync(client, lessonId, 20);

        await AnswerAllOkAsync(client, session.GetProperty("id").GetGuid(), QuestionIds(session));

        session.GetProperty("isTestMode").GetBoolean().Should().BeTrue();
        (await ReadAttemptsAsync(factory, session.GetProperty("id").GetGuid())).Should().HaveCount(11);
    }

    private static List<Guid> QuestionIds(JsonElement session) => session.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("questionId").GetGuid()).ToList();

    private static async Task AnswerAllOkAsync(HttpClient client, Guid sessionId, IEnumerable<Guid> questionIds)
    {
        foreach (var questionId in questionIds)
        {
            using var response = await AnswerAsync(client, sessionId, questionId, "b").ConfigureAwait(false);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }
}
