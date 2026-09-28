using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Schemas;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Sessions.Exams;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Exams;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elmanhg.Tests.Integration.Persistence;

public sealed class ExamSessionPersistenceTests(ApiFactory factory)
{
    [Fact]
    public async Task SavedAnswer_RoundTrips_AsJsonb()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, TestContext.Current.CancellationToken);
        var (_, unitId, _, _) = await ExamTestData.SeedExamUnitAsync(factory, 1);
        var sessionId = await AddExamAsync(student.Id, unitId);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var session = await context.Sessions.Include(x => x.Items).SingleAsync(x => x.Id == sessionId, TestContext.Current.CancellationToken);
            session.SaveExamAnswer(session.Items[0], SessionBuilder.AnswerB, TimeSpan.FromSeconds(30), DateTimeOffset.UtcNow);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var readScope = factory.Services.CreateScope();
        var readContext = readScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dataType = await readContext.Database
            .SqlQuery<string>($"SELECT data_type AS \"Value\" FROM information_schema.columns WHERE table_name = {"SessionItems"} AND column_name = {"SavedAnswer"}")
            .SingleAsync(TestContext.Current.CancellationToken);
        dataType.Should().Be("jsonb");
        var item = await readContext.SessionItems.AsNoTracking().SingleAsync(x => x.SessionId == sessionId, TestContext.Current.CancellationToken);
        QuestionJson.AreEquivalent(item.SavedAnswer ?? string.Empty, SessionBuilder.AnswerB).Should().BeTrue();
        item.AnswerSavedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SecondOpenExam_ForStudent_ThrowsExamAlreadyInProgress()
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, TestContext.Current.CancellationToken);
        var (_, firstUnitId, _, _) = await ExamTestData.SeedExamUnitAsync(factory, 1);
        var (_, secondUnitId, _, _) = await ExamTestData.SeedExamUnitAsync(factory, 1);
        await AddExamAsync(student.Id, firstUnitId);

        var act = () => AddExamAsync(student.Id, secondUnitId);

        (await act.Should().ThrowAsync<ConflictCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.ExamAlreadyInProgress);
    }

    [Fact]
    public async Task GetServableExamCandidatesAsync_ReturnsOnlyServableQuestionsOfTheUnits()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Physics", 1, cancellationToken);
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, cancellationToken);
        var otherUnitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Optics", 2, cancellationToken);
        var publishedLessonId = await ContentTestData.SeedLessonInStateAsync(factory, unitId, "Published", 1, LessonState.Published, cancellationToken);
        var draftLessonId = await ContentTestData.SeedLessonInStateAsync(factory, unitId, "Draft", 2, LessonState.Draft, cancellationToken);
        var otherLessonId = await ContentTestData.SeedLessonInStateAsync(factory, otherUnitId, "Other", 1, LessonState.Published, cancellationToken);
        var servableId = await QuestionTestData.SeedQuestionAsync(factory, publishedLessonId, approved: true, cancellationToken);
        await QuestionTestData.SeedQuestionAsync(factory, publishedLessonId, approved: false, cancellationToken);
        await QuestionTestData.SeedRetiredQuestionAsync(factory, publishedLessonId, cancellationToken);
        await QuestionTestData.SeedQuestionAsync(factory, draftLessonId, approved: true, cancellationToken);
        await QuestionTestData.SeedQuestionAsync(factory, otherLessonId, approved: true, cancellationToken);
        using var scope = factory.Services.CreateScope();
        var questionRepository = scope.ServiceProvider.GetRequiredService<IQuestionRepository>();

        var result = await questionRepository.GetServableExamCandidatesAsync([unitId], cancellationToken);

        result.Should().Equal(new ExamCandidate(servableId, publishedLessonId, QuestionType.Mcq, QuestionDifficulty.Medium));
    }

    private async Task<Guid> AddExamAsync(Guid studentId, Guid unitId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var unit = await context.Units.AsNoTracking().SingleAsync(x => x.Id == unitId, TestContext.Current.CancellationToken).ConfigureAwait(false);
        var lessons = await context.Lessons.AsNoTracking().Where(x => x.UnitId == unitId).ToListAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        var lessonIds = lessons.Select(x => x.Id).ToList();
        var questions = await context.Questions.AsNoTracking().Where(x => lessonIds.Contains(x.LessonId)).ToListAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        var blueprint = await context.ExamBlueprints.AsNoTracking().SingleAsync(x => x.UnitId == unitId, TestContext.Current.CancellationToken).ConfigureAwait(false);
        var session = Session.StartUnitExam(studentId, unit, blueprint, questions, lessons, false, DateTimeOffset.UtcNow);
        context.Sessions.Add(session);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        return session.Id;
    }
}
