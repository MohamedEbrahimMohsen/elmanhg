using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.TrainingData;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Infrastructure;
using Elmanhg.Tests.Integration.TeacherInbox;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Elmanhg.Tests.Integration.Sessions.SessionTestData;
using static Elmanhg.Tests.Integration.TrainingData.TrainingDataTestData;

namespace Elmanhg.Tests.Integration.Persistence;

public sealed class TrainingRecordExportPageTests(ApiFactory factory)
{
    private static readonly DateTimeOffset Day = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AttemptPage_WithCursor_ReturnsRowsAfterCursorInOrder()
    {
        var subjectId = await SeedAttemptsAsync(3);
        var filter = new TrainingRecordFilter(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1), subjectId);
        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAttemptTrainingRecordRepository>();
        var first = await repository.GetExportPageAsync(filter, null, 2, CancellationToken);

        var second = await repository.GetExportPageAsync(filter, new TrainingRecordCursor(first[^1].OccurredAt, first[^1].Id), 2, CancellationToken);

        first.Should().HaveCount(2).And.BeInAscendingOrder(x => x.OccurredAt);
        second.Should().ContainSingle();
        first.Concat(second).Select(x => x.Id).Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData(24.0, TeacherThreadTrainingTrigger.RatedAfterClose)]
    [InlineData(15.5, TeacherThreadTrainingTrigger.Closed)]
    public async Task TeacherThreadPage_ClosedThenRated_ReturnsLatestSnapshotInsideRange(double rangeHours, TeacherThreadTrainingTrigger expected)
    {
        var subjectId = await ScopeTestData.SeedSubjectAsync(factory, $"Physics {Guid.NewGuid():N}", CancellationToken);
        var teacher = await ScopeTestData.SeedTeacherAsync(factory, CancellationToken);
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken);
        var thread = new TeacherThreadBuilder().ForStudent(student.Id).WithContext(TeacherInboxTestData.ContextFor(subjectId)).AnsweredBy(teacher.Id).FinalReplied().Rated(5).Build();
        await TeacherInboxTestData.SeedThreadAsync(factory, thread);
        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITeacherThreadTrainingRecordRepository>();

        var page = await repository.GetExportPageAsync(new TrainingRecordFilter(Day, Day.AddHours(rangeHours), subjectId), null, 10, CancellationToken);

        page.Should().ContainSingle().Which.Trigger.Should().Be(expected);
    }

    private async Task<Guid> SeedAttemptsAsync(int count)
    {
        var (lessonId, questionIds) = await SeedServableLessonAsync(factory, count).ConfigureAwait(false);
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken).ConfigureAwait(false);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var lesson = await context.Lessons.AsNoTracking().SingleAsync(x => x.Id == lessonId, CancellationToken).ConfigureAwait(false);
            var questions = await context.Questions.AsNoTracking().Where(x => questionIds.Contains(x.Id)).ToListAsync(CancellationToken).ConfigureAwait(false);
            var session = Session.StartQuiz(student.Id, lesson, questions, isTestMode: false);
            foreach (var item in session.Items.ToList())
            {
                session.RecordAttempt(item, SessionBuilder.AnswerB, SessionBuilder.Grade(1m), 0);
            }

            context.Sessions.Add(session);
            await context.SaveChangesAsync(CancellationToken).ConfigureAwait(false);
        }

        return (await ReadLessonScopeAsync(factory, lessonId).ConfigureAwait(false)).SubjectId;
    }
}
