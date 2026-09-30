using Core.Errors;
using Elmanhg.Domain.Sessions;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;
using static Elmanhg.Tests.Integration.TrainingData.TrainingDataTestData;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Integration.TrainingData;

public sealed class AttemptTrainingRecordTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SubmitAnswer_StudentQuiz_WritesRecordWithHashedStudentAndPlacement()
    {
        var (lessonId, questionIds) = await SeedServableLessonAsync(factory, 1);
        var (student, client) = await SignedInStudentAsync(factory);
        var sessionId = (await StartQuizAsync(client, lessonId)).GetProperty("id").GetGuid();

        using var response = await AnswerAsync(client, sessionId, questionIds[0], "b");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var attempt = (await ReadAttemptsAsync(factory, sessionId)).Single();
        var record = (await ReadAttemptRecordsAsync(factory, attempt.Id)).Should().ContainSingle().Subject;
        record.StudentHash.Should().Be(ExpectedHash(student.Id)).And.NotContain(student.Id.ToString("N")).And.NotContain(student.Id.ToString("D"));
        var (subjectId, unitId) = await ReadLessonScopeAsync(factory, lessonId);
        (record.SubjectId, record.UnitId, record.LessonId, record.QuestionId).Should().Be((subjectId, unitId, lessonId, questionIds[0]));
        (record.SessionKind, record.QuestionVersion, record.OccurredAt).Should().Be((SessionKind.Quiz, attempt.QuestionVersion, attempt.CreatedAt));
        (record.Answer, record.Score, record.NormalisedScore, record.GradedBy).Should().Be((attempt.Answer, attempt.Score, attempt.NormalisedScore, attempt.GradedBy));
    }

    [Fact]
    public async Task SubmitAnswer_SameAnswerTwice_WritesOneRecord()
    {
        var (lessonId, questionIds) = await SeedServableLessonAsync(factory, 1);
        var (student, client) = await SignedInStudentAsync(factory);
        var sessionId = (await StartQuizAsync(client, lessonId)).GetProperty("id").GetGuid();
        using var first = await AnswerAsync(client, sessionId, questionIds[0], "b");

        using var second = await AnswerAsync(client, sessionId, questionIds[0], "b");

        (first.StatusCode, second.StatusCode).Should().Be((HttpStatusCode.OK, HttpStatusCode.OK));
        (await ReadAttemptRecordsOfStudentAsync(factory, ExpectedHash(student.Id))).Should().ContainSingle();
    }

    [Fact]
    public async Task SubmitAnswer_AdminTestMode_WritesNoRecord()
    {
        var (lessonId, questionIds) = await SeedServableLessonAsync(factory, 1);
        var admin = await ScopeTestData.SeedAdminAsync(factory, CancellationToken);
        using var client = await ScopeTestData.SignedInClientAsync(factory, admin, CancellationToken);
        var sessionId = (await StartQuizAsync(client, lessonId)).GetProperty("id").GetGuid();

        using var response = await AnswerAsync(client, sessionId, questionIds[0], "b");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var attempt = (await ReadAttemptsAsync(factory, sessionId)).Should().ContainSingle().Subject;
        (await ReadAttemptRecordsAsync(factory, attempt.Id)).Should().BeEmpty();
        (await ReadAttemptRecordsOfStudentAsync(factory, ExpectedHash(admin.Id))).Should().BeEmpty();
    }

    [Fact]
    public async Task SaveChanges_ConcurrentFirstAnswers_KeepsOneRecord()
    {
        var (lessonId, questionIds) = await SeedServableLessonAsync(factory, 1);
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var sessionId = await AddSessionAsync(student.Id, lessonId, questionIds[0]);
        using var firstScope = factory.Services.CreateScope();
        using var secondScope = factory.Services.CreateScope();
        var (firstContext, firstSession) = await LoadAsync(firstScope, sessionId);
        var (secondContext, secondSession) = await LoadAsync(secondScope, sessionId);
        firstSession.RecordAttempt(firstSession.Items[0], SessionBuilder.AnswerB, SessionBuilder.Grade(1m), 0);
        secondSession.RecordAttempt(secondSession.Items[0], SessionBuilder.AnswerA, SessionBuilder.Grade(0m), 0);
        await firstContext.SaveChangesAsync(CancellationToken);

        var act = () => secondContext.SaveChangesAsync(CancellationToken);

        (await act.Should().ThrowAsync<ConflictCoreException>()).Which.ErrorCode.Should().Be(DomainErrorCodes.SessionQuestionAlreadyAnswered);
        var attempt = (await ReadAttemptsAsync(factory, sessionId)).Should().ContainSingle().Subject;
        var record = (await ReadAttemptRecordsOfStudentAsync(factory, ExpectedHash(student.Id))).Should().ContainSingle(x => x.QuestionId == questionIds[0]).Subject;
        record.AttemptId.Should().Be(attempt.Id);
    }

    private async Task<Guid> AddSessionAsync(Guid studentId, Guid lessonId, Guid questionId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lesson = await context.Lessons.AsNoTracking().SingleAsync(x => x.Id == lessonId, CancellationToken).ConfigureAwait(false);
        var question = await context.Questions.AsNoTracking().SingleAsync(x => x.Id == questionId, CancellationToken).ConfigureAwait(false);
        var session = Session.StartQuiz(studentId, lesson, [question], isTestMode: false);
        context.Sessions.Add(session);
        await context.SaveChangesAsync(CancellationToken).ConfigureAwait(false);
        return session.Id;
    }

    private static async Task<(AppDbContext Context, Session Session)> LoadAsync(IServiceScope scope, Guid sessionId)
    {
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var session = await context.Sessions.Include(x => x.Items).Include(x => x.Attempts).AsSplitQuery().SingleAsync(x => x.Id == sessionId, CancellationToken).ConfigureAwait(false);
        return (context, session);
    }
}
