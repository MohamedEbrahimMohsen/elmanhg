using Elmanhg.Application.MathStepGrading.ApplyMathStepGrade;
using Elmanhg.Application.MathStepGrading.CheckMathStepAnswer;
using Elmanhg.Application.MathStepGrading.GradeMathSteps;
using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.MathStepGrading;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Grading;
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

namespace Elmanhg.Tests.Integration.MathStepGrading;

public static class MathStepGradingTestData
{
    public const string Answer = """{"steps":["2x = 4"],"finalAnswer":"x = 2"}""";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public static string Route(Guid sessionId, Guid questionId) => $"/api/sessions/{sessionId}/questions/{questionId}/math-step-grade";

    public static async Task<(Guid MathStepGradeId, Guid SessionId, Guid QuestionId)> SeedPendingAsync(ApiFactory factory, Guid studentId)
    {
        var (lessonId, questionIds) = await SessionTestData.SeedServableLessonAsync(factory, 1).ConfigureAwait(false);
        var mathId = await SeedApprovedStepGradedAsync(factory, lessonId).ConfigureAwait(false);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lesson = await context.Lessons.AsNoTracking().SingleAsync(x => x.Id == lessonId, CancellationToken).ConfigureAwait(false);
        var questions = await context.Questions.AsNoTracking().Where(x => x.Id == questionIds[0] || x.Id == mathId).ToListAsync(CancellationToken).ConfigureAwait(false);
        var session = Session.StartQuiz(studentId, lesson, questions, isTestMode: false);
        var item = session.GetItem(mathId)!;
        var submission = session.SubmitForAiGrading(item, Answer, 0);
        context.Sessions.Add(session);
        var subjectId = questions.Single(x => x.Id == mathId).SubjectId;
        var grade = MathStepGrade.Request(studentId, session.Id, subjectId, mathId, item.QuestionVersion, item.MaxScore, Answer, MathAnswerVerdict.Equivalent, submission.SubmittedAt.AddSeconds(-1), 0);
        context.MathStepGrades.Add(grade);
        await context.SaveChangesAsync(CancellationToken).ConfigureAwait(false);
        return (grade.Id, session.Id, mathId);
    }

    public static async Task GradeAsync(ApiFactory factory, Guid mathStepGradeId)
    {
        using var scope = factory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(new CheckMathStepAnswerCommand(mathStepGradeId), CancellationToken).ConfigureAwait(false);
        await sender.Send(new GradeMathStepsCommand(mathStepGradeId), CancellationToken).ConfigureAwait(false);
        await sender.Send(new ApplyMathStepGradeCommand(mathStepGradeId), CancellationToken).ConfigureAwait(false);
    }

    public static async Task<List<MathStepGrade>> ReadGradesForAsync(ApiFactory factory, Guid sessionId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.MathStepGrades.AsNoTracking().Where(x => x.SessionId == sessionId).ToListAsync(CancellationToken).ConfigureAwait(false);
    }

    public static async Task<(Guid UnitId, Guid McqQuestionId, Guid MathQuestionId)> SeedMathUnitExamAsync(ApiFactory factory)
    {
        var subjectId = await ContentTestData.SeedSubjectAsync(factory, "Mathematics", 1, CancellationToken).ConfigureAwait(false);
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Algebra", 1, CancellationToken).ConfigureAwait(false);
        var lessonId = await ContentTestData.SeedLessonInStateAsync(factory, unitId, "Linear equations", 1, LessonState.Published, CancellationToken).ConfigureAwait(false);
        var mcqId = await QuestionTestData.SeedQuestionAsync(factory, lessonId, approved: true, CancellationToken).ConfigureAwait(false);
        var mathId = await SeedApprovedStepGradedAsync(factory, lessonId).ConfigureAwait(false);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var unit = await context.Units.SingleAsync(x => x.Id == unitId, CancellationToken).ConfigureAwait(false);
        context.ExamBlueprints.Add(ExamBlueprint.CreateForUnit(unit, new ExamBlueprintShape([new ExamTypeCount(QuestionType.Mcq, 1), new ExamTypeCount(QuestionType.MathSteps, 1)], null, 30, 50), ExamBlueprintBuilder.Plenty(), Guid.NewGuid()));
        await context.SaveChangesAsync(CancellationToken).ConfigureAwait(false);
        return (unitId, mcqId, mathId);
    }

    public static async Task<Guid> SeedApprovedStepGradedAsync(ApiFactory factory, Guid lessonId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lesson = await context.Lessons.Include(x => x.Objectives).AsNoTracking().SingleAsync(x => x.Id == lessonId, CancellationToken).ConfigureAwait(false);
        var unit = await context.Units.AsNoTracking().SingleAsync(x => x.Id == lesson.UnitId, CancellationToken).ConfigureAwait(false);
        var subject = await context.Subjects.AsNoTracking().SingleAsync(x => x.Id == unit.SubjectId, CancellationToken).ConfigureAwait(false);
        var creator = Guid.NewGuid();
        var question = Question.Create(lesson, unit, QuestionType.MathSteps, QuestionBuilder.MathStepsContent(QuestionBuilder.MathStepsGradedSpecJson), new QuestionMetadata(QuestionDifficulty.Medium, null, []), creator);
        question.Approve(TeacherSubject.Create(User.CreateTeacher("Teacher", $"{Guid.NewGuid():N}@example.com"), subject, creator), question.Version);
        context.Questions.Add(question);
        await context.SaveChangesAsync(CancellationToken).ConfigureAwait(false);
        return question.Id;
    }
}
