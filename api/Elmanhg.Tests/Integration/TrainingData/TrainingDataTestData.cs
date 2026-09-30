using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.TrainingData;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Avatar;
using Elmanhg.Tests.Integration.Infrastructure;
using Elmanhg.Tests.Integration.Sessions;
using Elmanhg.Tests.Integration.TeacherInbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;

namespace Elmanhg.Tests.Integration.TrainingData;

public static class TrainingDataTestData
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public static string ExpectedHash(Guid studentId) => Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(ApiFactory.TestStudentIdHashKey), Encoding.UTF8.GetBytes(studentId.ToString("D"))));

    public static Task<List<AttemptTrainingRecord>> ReadAttemptRecordsAsync(ApiFactory factory, Guid attemptId) => ReadAsync<AttemptTrainingRecord>(factory, x => x.AttemptId == attemptId);

    public static Task<List<AttemptTrainingRecord>> ReadAttemptRecordsOfStudentAsync(ApiFactory factory, string studentHash) => ReadAsync<AttemptTrainingRecord>(factory, x => x.StudentHash == studentHash);

    public static Task<List<AvatarTrainingRecord>> ReadAvatarRecordsAsync(ApiFactory factory, string studentHash) => ReadAsync<AvatarTrainingRecord>(factory, x => x.StudentHash == studentHash);

    public static Task<List<TeacherThreadTrainingRecord>> ReadThreadRecordsAsync(ApiFactory factory, Guid threadId) => ReadAsync<TeacherThreadTrainingRecord>(factory, x => x.ThreadId == threadId);

    public static async Task<(Guid SubjectId, Guid UnitId)> ReadLessonScopeAsync(ApiFactory factory, Guid lessonId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var unitId = await context.Lessons.AsNoTracking().Where(x => x.Id == lessonId).Select(x => x.UnitId).SingleAsync(CancellationToken).ConfigureAwait(false);
        var subjectId = await context.Units.AsNoTracking().Where(x => x.Id == unitId).Select(x => x.SubjectId).SingleAsync(CancellationToken).ConfigureAwait(false);
        return (subjectId, unitId);
    }

    public static async Task<Guid> SeedRecordIdAsync(ApiFactory factory, string table)
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken).ConfigureAwait(false);
        var records = table switch
        {
            AppDbContext.AttemptTrainingRecordsTable => (await SeedAttemptRecordsAsync(factory, student.Id).ConfigureAwait(false)).Select(x => x.Id),
            AppDbContext.AvatarTrainingRecordsTable => (await SeedAvatarRecordsAsync(factory, student.Id).ConfigureAwait(false)).Select(x => x.Id),
            AppDbContext.TeacherThreadTrainingRecordsTable => (await SeedThreadRecordsAsync(factory, student.Id).ConfigureAwait(false)).Select(x => x.Id),
            _ => throw new ArgumentOutOfRangeException(nameof(table)),
        };
        return records.Single();
    }

    private static async Task<List<AttemptTrainingRecord>> SeedAttemptRecordsAsync(ApiFactory factory, Guid studentId)
    {
        var (lessonId, questionIds) = await SessionTestData.SeedServableLessonAsync(factory, 1).ConfigureAwait(false);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var lesson = await context.Lessons.AsNoTracking().SingleAsync(x => x.Id == lessonId, CancellationToken).ConfigureAwait(false);
            var question = await context.Questions.AsNoTracking().SingleAsync(x => x.Id == questionIds[0], CancellationToken).ConfigureAwait(false);
            var session = Session.StartQuiz(studentId, lesson, [question], isTestMode: false);
            session.RecordAttempt(session.Items[0], SessionBuilder.AnswerB, SessionBuilder.Grade(1m), 0);
            context.Sessions.Add(session);
            await context.SaveChangesAsync(CancellationToken).ConfigureAwait(false);
        }

        return await ReadAttemptRecordsOfStudentAsync(factory, ExpectedHash(studentId)).ConfigureAwait(false);
    }

    private static async Task<List<AvatarTrainingRecord>> SeedAvatarRecordsAsync(ApiFactory factory, Guid studentId)
    {
        await AvatarTestData.SeedConversationAsync(factory, new AvatarConversationBuilder().ForStudent(studentId).WithExchange("question", "reply").Build()).ConfigureAwait(false);
        return await ReadAvatarRecordsAsync(factory, ExpectedHash(studentId)).ConfigureAwait(false);
    }

    private static async Task<List<TeacherThreadTrainingRecord>> SeedThreadRecordsAsync(ApiFactory factory, Guid studentId)
    {
        var subjectId = await ScopeTestData.SeedSubjectAsync(factory, $"Physics {Guid.NewGuid():N}", CancellationToken).ConfigureAwait(false);
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken).ConfigureAwait(false);
        var thread = await TeacherInboxTestData.SeedThreadAsync(factory, new TeacherThreadBuilder().ForStudent(studentId).WithContext(TeacherInboxTestData.ContextFor(subjectId)).AnsweredBy(teacher.Id).FinalReplied().Build()).ConfigureAwait(false);
        return await ReadThreadRecordsAsync(factory, thread.Id).ConfigureAwait(false);
    }

    private static async Task<List<T>> ReadAsync<T>(ApiFactory factory, Expression<Func<T, bool>> predicate) where T : class
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.Set<T>().AsNoTracking().Where(predicate).ToListAsync(CancellationToken).ConfigureAwait(false);
    }
}
