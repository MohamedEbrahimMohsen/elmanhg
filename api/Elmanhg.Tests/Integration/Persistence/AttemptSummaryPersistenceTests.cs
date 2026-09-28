using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Sessions.Selection;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using Elmanhg.Tests.Integration.Sessions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elmanhg.Tests.Integration.Persistence;

public sealed class AttemptSummaryPersistenceTests(ApiFactory factory)
{
    private const decimal Threshold = 0.8m;

    [Fact]
    public async Task GetAttemptSummariesAsync_MixedHistory_AggregatesPerQuestion()
    {
        var (studentId, lessonId, questionIds) = await SeedAsync(3);
        await RunSessionAsync(studentId, lessonId, [(questionIds[0], 0m), (questionIds[1], 1m), (questionIds[2], 0.5m)]);
        await RunSessionAsync(studentId, lessonId, [(questionIds[0], 1m), (questionIds[1], 0m)]);

        var result = (await QueryAsync(studentId, questionIds)).ToDictionary(x => x.QuestionId);

        result.Should().HaveCount(3);
        var first = result[questionIds[0]];
        (first.AttemptCount, first.CorrectCount, first.IsLastAttemptCorrect, first.Bucket).Should().Be((2, 1, true, QuestionSelectionBucket.CorrectOnce));
        var second = result[questionIds[1]];
        (second.AttemptCount, second.CorrectCount, second.Bucket).Should().Be((2, 1, QuestionSelectionBucket.LastWrong));
        second.LastCorrectAt.Should().BeBefore(second.LastAttemptedAt);
        var third = result[questionIds[2]];
        (third.AttemptCount, third.CorrectCount, third.LastCorrectAt, third.Bucket).Should().Be((1, 0, (DateTimeOffset?)null, QuestionSelectionBucket.LastWrong));
    }

    [Fact]
    public async Task GetAttemptSummariesAsync_OtherStudentAndUnrequestedQuestion_AreExcluded()
    {
        var (studentId, lessonId, questionIds) = await SeedAsync(2);
        var other = await ScopeTestData.SeedStudentAsync(factory, TestContext.Current.CancellationToken);
        await RunSessionAsync(other.Id, lessonId, [(questionIds[0], 1m)]);
        await RunSessionAsync(studentId, lessonId, [(questionIds[1], 1m)]);

        var result = await QueryAsync(studentId, [questionIds[0]]);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAttemptSummariesAsync_TestModeSession_Counts()
    {
        var (studentId, lessonId, questionIds) = await SeedAsync(1);
        await RunSessionAsync(studentId, lessonId, [(questionIds[0], 1m)], isTestMode: true);

        var result = await QueryAsync(studentId, questionIds);

        result.Should().ContainSingle().Which.AttemptCount.Should().Be(1);
    }

    [Fact]
    public async Task GetAttemptSummariesAsync_OlderQuestionVersion_Counts()
    {
        var (studentId, lessonId, questionIds) = await SeedAsync(1);
        await RunSessionAsync(studentId, lessonId, [(questionIds[0], 0m)]);
        await SessionTestData.EditQuestionContentAsync(factory, questionIds[0], QuestionBuilder.McqContent() with { GradingSpec = """{"correctOptionId":"a"}""" });

        var result = await QueryAsync(studentId, questionIds);

        result.Should().ContainSingle().Which.AttemptCount.Should().Be(1);
    }

    private async Task<(Guid StudentId, Guid LessonId, List<Guid> QuestionIds)> SeedAsync(int questionCount)
    {
        var (lessonId, questionIds) = await SessionTestData.SeedServableLessonAsync(factory, questionCount).ConfigureAwait(false);
        var student = await ScopeTestData.SeedStudentAsync(factory, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return (student.Id, lessonId, questionIds);
    }

    private async Task RunSessionAsync(Guid studentId, Guid lessonId, List<(Guid QuestionId, decimal Grade)> answers, bool isTestMode = false)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var questionIds = answers.Select(x => x.QuestionId).ToList();
        var lesson = await context.Lessons.AsNoTracking().SingleAsync(x => x.Id == lessonId, TestContext.Current.CancellationToken).ConfigureAwait(false);
        var questions = await context.Questions.AsNoTracking().Where(x => questionIds.Contains(x.Id)).ToListAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        var session = Session.StartQuiz(studentId, lesson, questions, isTestMode);
        foreach (var (questionId, grade) in answers)
        {
            session.RecordAttempt(session.GetItem(questionId)!, SessionBuilder.AnswerB, SessionBuilder.Grade(grade), 0);
        }

        session.Submit();
        context.Sessions.Add(session);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    private async Task<List<QuestionAttemptSummary>> QueryAsync(Guid studentId, List<Guid> questionIds)
    {
        using var scope = factory.Services.CreateScope();
        var sessionRepository = scope.ServiceProvider.GetRequiredService<ISessionRepository>();
        return await sessionRepository.GetAttemptSummariesAsync(studentId, questionIds, Threshold, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }
}
