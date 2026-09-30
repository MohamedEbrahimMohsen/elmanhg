using Elmanhg.Domain.EssayGrading;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.MathStepGrading;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Teachers;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Authorization;
using Elmanhg.Tests.Integration.Content;
using Elmanhg.Tests.Integration.Infrastructure;
using Elmanhg.Tests.Integration.TeacherInbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elmanhg.Tests.Integration.GradeReviews;

public static class GradeReviewTestData
{
    public const string EssayAnswer = """{"text":"القصور الذاتي هو ممانعة الجسم لتغيير حالته الحركية."}""";
    public const string MathAnswer = """{"steps":["2x = 4"],"finalAnswer":"x = 2"}""";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public static string QueueRoute(Guid subjectId, string kind) => $"/api/subjects/{subjectId}/grade-reviews?kind={kind}";

    public static string EssayRoute(Guid subjectId, Guid gradeId) => $"/api/subjects/{subjectId}/grade-reviews/essays/{gradeId}";

    public static string MathStepsRoute(Guid subjectId, Guid gradeId) => $"/api/subjects/{subjectId}/grade-reviews/math-steps/{gradeId}";

    public static async Task<(Guid GradeId, Guid SessionId, Guid QuestionId, User Student)> SeedInReviewEssayAsync(ApiFactory factory, Guid subjectId, bool gradingFailed = false, bool isTestMode = false)
    {
        var (student, lesson, question, context, scope) = await SeedQuestionAsync(factory, subjectId, QuestionType.Essay, QuestionBuilder.EssayContent()).ConfigureAwait(false);
        using (scope)
        {
            var session = Session.StartQuiz(student.Id, lesson, [question], isTestMode);
            var item = session.GetItem(question.Id)!;
            var submission = session.SubmitEssay(item, EssayAnswer, 0);
            context.Sessions.Add(session);
            await context.SaveChangesAsync(CancellationToken).ConfigureAwait(false);
            var grade = EssayGrade.Request(student.Id, session.Id, subjectId, question.Id, item.QuestionVersion, item.MaxScore, "القصور الذاتي هو ممانعة الجسم لتغيير حالته الحركية.", submission.SubmittedAt, 0);
            if (gradingFailed)
            {
                grade.FailAttempt("ESSAY_GRADING_UNAVAILABLE", submission.SubmittedAt.AddSeconds(5), 1, TimeSpan.FromSeconds(30));
            }
            else
            {
                grade.Complete(EssayGradeBuilder.Assessment(0.5m), new QuestionGrade(2.5m, 0.5m, GradeOutcome.Partial, null), 0.7m, submission.SubmittedAt.AddSeconds(40));
            }

            context.EssayGrades.Add(grade);
            await context.SaveChangesAsync(CancellationToken).ConfigureAwait(false);
            return (grade.Id, session.Id, question.Id, student);
        }
    }

    public static async Task<(Guid GradeId, Guid SessionId, Guid QuestionId, User Student)> SeedInReviewMathStepAsync(ApiFactory factory, Guid subjectId, bool isTestMode = false)
    {
        var (student, lesson, question, context, scope) = await SeedQuestionAsync(factory, subjectId, QuestionType.MathSteps, QuestionBuilder.MathStepsContent()).ConfigureAwait(false);
        using (scope)
        {
            var session = Session.StartQuiz(student.Id, lesson, [question], isTestMode);
            var item = session.GetItem(question.Id)!;
            var submission = session.SubmitForAiGrading(item, MathAnswer, 0);
            context.Sessions.Add(session);
            await context.SaveChangesAsync(CancellationToken).ConfigureAwait(false);
            var grade = MathStepGrade.Request(student.Id, session.Id, subjectId, question.Id, item.QuestionVersion, item.MaxScore, MathAnswer, null, submission.SubmittedAt, 0);
            grade.FailAttempt("MATH_CHECK_UNAVAILABLE", submission.SubmittedAt.AddSeconds(5), 1, TimeSpan.FromSeconds(30));
            context.MathStepGrades.Add(grade);
            await context.SaveChangesAsync(CancellationToken).ConfigureAwait(false);
            return (grade.Id, session.Id, question.Id, student);
        }
    }

    public static Task<(User Teacher, HttpClient Client)> ReviewClientAsync(ApiFactory factory, Guid subjectId) => TeacherInboxTestData.SignedInTeacherForAsync(factory, subjectId);

    private static async Task<(User Student, Lesson Lesson, Question Question, AppDbContext Context, IServiceScope Scope)> SeedQuestionAsync(ApiFactory factory, Guid subjectId, QuestionType type, QuestionContent content)
    {
        var student = await ScopeTestData.SeedStudentAsync(factory, CancellationToken).ConfigureAwait(false);
        var unitId = await ContentTestData.SeedUnitAsync(factory, subjectId, "Mechanics", 1, CancellationToken).ConfigureAwait(false);
        var lessonId = await ContentTestData.SeedLessonInStateAsync(factory, unitId, "Newton's laws", 1, LessonState.Published, CancellationToken).ConfigureAwait(false);
        var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lesson = await context.Lessons.Include(x => x.Objectives).AsNoTracking().SingleAsync(x => x.Id == lessonId, CancellationToken).ConfigureAwait(false);
        var unit = await context.Units.AsNoTracking().SingleAsync(x => x.Id == unitId, CancellationToken).ConfigureAwait(false);
        var subject = await context.Subjects.AsNoTracking().SingleAsync(x => x.Id == subjectId, CancellationToken).ConfigureAwait(false);
        var creator = Guid.NewGuid();
        var question = Question.Create(lesson, unit, type, content, new QuestionMetadata(QuestionDifficulty.Medium, null, []), creator);
        question.Approve(TeacherSubject.Create(User.CreateTeacher("Teacher", $"{Guid.NewGuid():N}@example.com"), subject, creator), question.Version);
        context.Questions.Add(question);
        await context.SaveChangesAsync(CancellationToken).ConfigureAwait(false);
        return (student, lesson, question, context, scope);
    }
}
