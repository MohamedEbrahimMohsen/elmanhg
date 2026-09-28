using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using Elmanhg.Tests.Integration.Sessions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Integration.Persistence;

public sealed class SessionPersistenceTests(ApiFactory factory)
{
    [Fact]
    public async Task Migrate_Attempts_HasStudentQuestionIndex()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var definition = await context.Database
            .SqlQuery<string>($"SELECT indexdef AS \"Value\" FROM pg_indexes WHERE indexname = {"IX_Attempts_StudentId_QuestionId_CreatedAt"}")
            .SingleAsync(TestContext.Current.CancellationToken);

        definition.Should().Contain("\"StudentId\", \"QuestionId\", \"CreatedAt\"");
    }

    [Fact]
    public async Task SaveChanges_SecondOpenSessionSameScope_ThrowsSessionAlreadyInProgress()
    {
        var (studentId, lessonId, questionId) = await SeedAsync();
        await AddSessionAsync(studentId, lessonId, questionId);

        var act = () => AddSessionAsync(studentId, lessonId, questionId);

        (await act.Should().ThrowAsync<ConflictCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.SessionAlreadyInProgress);
    }

    [Fact]
    public async Task SaveChanges_ConcurrentFirstAnswers_ThrowsSessionQuestionAlreadyAnswered()
    {
        var (studentId, lessonId, questionId) = await SeedAsync();
        var sessionId = await AddSessionAsync(studentId, lessonId, questionId);
        using var firstScope = factory.Services.CreateScope();
        using var secondScope = factory.Services.CreateScope();
        var (firstContext, firstSession) = await LoadAsync(firstScope, sessionId);
        var (secondContext, secondSession) = await LoadAsync(secondScope, sessionId);
        firstSession.RecordAttempt(firstSession.Items[0], SessionBuilder.AnswerB, SessionBuilder.Grade(1m), 0);
        secondSession.RecordAttempt(secondSession.Items[0], SessionBuilder.AnswerA, SessionBuilder.Grade(0m), 0);
        await firstContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var act = () => secondContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ConflictCoreException>()).Which.ErrorCode.Should().Be(DomainErrorCodes.SessionQuestionAlreadyAnswered);
        (await SessionTestData.ReadAttemptsAsync(factory, sessionId)).Should().HaveCount(1);
    }

    [Fact]
    public async Task SaveChanges_AnswerAfterConcurrentFinish_ThrowsSessionModifiedConcurrently()
    {
        var (studentId, lessonId, questionId) = await SeedAsync();
        var sessionId = await AddSessionAsync(studentId, lessonId, questionId);
        using var answerScope = factory.Services.CreateScope();
        using var finishScope = factory.Services.CreateScope();
        var (answerContext, answerSession) = await LoadAsync(answerScope, sessionId);
        var (finishContext, finishSession) = await LoadAsync(finishScope, sessionId);
        answerSession.RecordAttempt(answerSession.Items[0], SessionBuilder.AnswerB, SessionBuilder.Grade(1m), 0);
        finishSession.Submit();
        await finishContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var act = () => answerContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ConflictCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.SessionModifiedConcurrently);
        (await SessionTestData.ReadAttemptsAsync(factory, sessionId)).Should().BeEmpty();
    }

    [Fact]
    public async Task SaveChanges_FinishAfterConcurrentAnswer_ThrowsSessionModifiedConcurrently()
    {
        var (studentId, lessonId, questionId) = await SeedAsync();
        var sessionId = await AddSessionAsync(studentId, lessonId, questionId);
        using var answerScope = factory.Services.CreateScope();
        using var finishScope = factory.Services.CreateScope();
        var (answerContext, answerSession) = await LoadAsync(answerScope, sessionId);
        var (finishContext, finishSession) = await LoadAsync(finishScope, sessionId);
        answerSession.RecordAttempt(answerSession.Items[0], SessionBuilder.AnswerB, SessionBuilder.Grade(1m), 0);
        finishSession.Submit();
        await answerContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var act = () => finishContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ConflictCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.SessionModifiedConcurrently);
        (await SessionTestData.ReadAttemptsAsync(factory, sessionId)).Should().HaveCount(1);
        using var readScope = factory.Services.CreateScope();
        var submittedAt = await readScope.ServiceProvider.GetRequiredService<AppDbContext>().Sessions.Where(x => x.Id == sessionId).Select(x => x.SubmittedAt).SingleAsync(TestContext.Current.CancellationToken);
        submittedAt.Should().BeNull();
    }

    [Fact]
    public async Task SaveChanges_SubmittedSessionSameScope_AllowsNewSession()
    {
        var (studentId, lessonId, questionId) = await SeedAsync();
        var firstId = await AddSessionAsync(studentId, lessonId, questionId);
        using (var scope = factory.Services.CreateScope())
        {
            var (context, first) = await LoadAsync(scope, firstId);
            first.Submit();
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await AddSessionAsync(studentId, lessonId, questionId);

        using var readScope = factory.Services.CreateScope();
        var count = await readScope.ServiceProvider.GetRequiredService<AppDbContext>().Sessions.CountAsync(x => x.StudentId == studentId, TestContext.Current.CancellationToken);
        count.Should().Be(2);
    }

    private async Task<(Guid StudentId, Guid LessonId, Guid QuestionId)> SeedAsync()
    {
        var (lessonId, questionIds) = await SessionTestData.SeedServableLessonAsync(factory, 1).ConfigureAwait(false);
        var student = await ScopeTestData.SeedStudentAsync(factory, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return (student.Id, lessonId, questionIds[0]);
    }

    private async Task<Guid> AddSessionAsync(Guid studentId, Guid lessonId, Guid questionId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lesson = await context.Lessons.AsNoTracking().SingleAsync(x => x.Id == lessonId, TestContext.Current.CancellationToken).ConfigureAwait(false);
        var question = await context.Questions.AsNoTracking().SingleAsync(x => x.Id == questionId, TestContext.Current.CancellationToken).ConfigureAwait(false);
        var session = Session.StartQuiz(studentId, lesson, [question], isTestMode: false);
        context.Sessions.Add(session);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        return session.Id;
    }

    private static async Task<(AppDbContext Context, Session Session)> LoadAsync(IServiceScope scope, Guid sessionId)
    {
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var session = await context.Sessions.Include(x => x.Items).Include(x => x.Attempts).AsSplitQuery().SingleAsync(x => x.Id == sessionId, TestContext.Current.CancellationToken).ConfigureAwait(false);
        return (context, session);
    }
}
