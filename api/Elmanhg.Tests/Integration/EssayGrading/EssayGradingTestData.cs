using Elmanhg.Application.EssayGrading.ApplyEssayGrade;
using Elmanhg.Application.EssayGrading.GradeEssay;
using Elmanhg.Domain.EssayGrading;
using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Teachers;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Infrastructure;
using Elmanhg.Tests.Integration.Sessions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elmanhg.Tests.Integration.EssayGrading;

public static class EssayGradingTestData
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public static async Task<(Guid EssayGradeId, Guid SessionId, Guid QuestionId)> SeedPendingAsync(ApiFactory factory, Guid studentId, bool isTestMode = false)
    {
        var (lessonId, questionIds) = await SessionTestData.SeedServableLessonAsync(factory, 1).ConfigureAwait(false);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lesson = await context.Lessons.AsNoTracking().Include(x => x.Objectives).SingleAsync(x => x.Id == lessonId, CancellationToken).ConfigureAwait(false);
        var unit = await context.Units.AsNoTracking().SingleAsync(x => x.Id == lesson.UnitId, CancellationToken).ConfigureAwait(false);
        var subject = await context.Subjects.AsNoTracking().SingleAsync(x => x.Id == unit.SubjectId, CancellationToken).ConfigureAwait(false);
        var mcq = await context.Questions.AsNoTracking().SingleAsync(x => x.Id == questionIds[0], CancellationToken).ConfigureAwait(false);
        var essay = Question.Create(lesson, unit, QuestionType.Essay, QuestionBuilder.EssayContent(), new QuestionMetadata(QuestionDifficulty.Medium, null, []), Guid.NewGuid());
        context.Questions.Add(essay);
        var session = Session.StartQuiz(studentId, lesson, [mcq], isTestMode);
        context.Sessions.Add(session);
        var grade = EssayGrade.Request(studentId, session.Id, subject.Id, essay.Id, essay.Version, 5, "القصور الذاتي هو ممانعة الجسم لتغيير حالته الحركية.", DateTimeOffset.UtcNow.AddSeconds(-1), 0);
        context.EssayGrades.Add(grade);
        await context.SaveChangesAsync(CancellationToken).ConfigureAwait(false);
        return (grade.Id, session.Id, essay.Id);
    }

    public static async Task GradeAsync(ApiFactory factory, Guid essayGradeId)
    {
        using var scope = factory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(new GradeEssayCommand(essayGradeId), CancellationToken).ConfigureAwait(false);
        await sender.Send(new ApplyEssayGradeCommand(essayGradeId), CancellationToken).ConfigureAwait(false);
    }

    public static async Task<EssayGrade> ReadAsync(ApiFactory factory, Guid essayGradeId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.EssayGrades.AsNoTracking().SingleAsync(x => x.Id == essayGradeId, CancellationToken).ConfigureAwait(false);
    }

    public static async Task<(Guid LessonId, Guid EssayQuestionId)> SeedServableEssayLessonAsync(ApiFactory factory)
    {
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Physics", 1, CancellationToken).ConfigureAwait(false);
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, CancellationToken).ConfigureAwait(false);
        var lessonId = await ContentTestData.SeedLessonInStateAsync(factory, unitId, "Newton's laws", 1, LessonState.Published, CancellationToken).ConfigureAwait(false);
        return (lessonId, await SeedApprovedEssayAsync(factory, lessonId).ConfigureAwait(false));
    }

    public static async Task<(Guid UnitId, Guid McqQuestionId, Guid EssayQuestionId)> SeedEssayUnitExamAsync(ApiFactory factory)
    {
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Physics", 1, CancellationToken).ConfigureAwait(false);
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, CancellationToken).ConfigureAwait(false);
        var lessonId = await ContentTestData.SeedLessonInStateAsync(factory, unitId, "Newton's laws", 1, LessonState.Published, CancellationToken).ConfigureAwait(false);
        var mcqId = await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: true, CancellationToken).ConfigureAwait(false);
        var essayId = await SeedApprovedEssayAsync(factory, lessonId).ConfigureAwait(false);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var unit = await context.Units.SingleAsync(x => x.Id == unitId, CancellationToken).ConfigureAwait(false);
        context.ExamBlueprints.Add(ExamBlueprint.CreateForUnit(unit, new ExamBlueprintShape([new ExamTypeCount(QuestionType.Mcq, 1), new ExamTypeCount(QuestionType.Essay, 1)], null, 30, 50), ExamBlueprintBuilder.Plenty(), Guid.NewGuid()));
        await context.SaveChangesAsync(CancellationToken).ConfigureAwait(false);
        return (unitId, mcqId, essayId);
    }

    public static async Task<EssayGrade> ReadGradeForAsync(ApiFactory factory, Guid sessionId, Guid questionId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.EssayGrades.AsNoTracking().SingleAsync(x => x.SessionId == sessionId && x.QuestionId == questionId, CancellationToken).ConfigureAwait(false);
    }

    private static async Task<Guid> SeedApprovedEssayAsync(ApiFactory factory, Guid lessonId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lesson = await context.Lessons.Include(x => x.Objectives).AsNoTracking().SingleAsync(x => x.Id == lessonId, CancellationToken).ConfigureAwait(false);
        var unit = await context.Units.AsNoTracking().SingleAsync(x => x.Id == lesson.UnitId, CancellationToken).ConfigureAwait(false);
        var subject = await context.Subjects.AsNoTracking().SingleAsync(x => x.Id == unit.SubjectId, CancellationToken).ConfigureAwait(false);
        var creator = Guid.NewGuid();
        var essay = Question.Create(lesson, unit, QuestionType.Essay, QuestionBuilder.EssayContent(), new QuestionMetadata(QuestionDifficulty.Medium, null, []), creator);
        essay.Approve(TeacherSubject.Create(User.CreateTeacher("Teacher", $"{Guid.NewGuid():N}@example.com"), subject, creator), essay.Version);
        context.Questions.Add(essay);
        await context.SaveChangesAsync(CancellationToken).ConfigureAwait(false);
        return essay.Id;
    }
}
