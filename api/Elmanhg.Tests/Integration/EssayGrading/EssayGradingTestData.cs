using Elmanhg.Application.EssayGrading.GradeEssay;
using Elmanhg.Domain.EssayGrading;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Infrastructure;
using Elmanhg.Tests.Integration.Sessions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elmanhg.Tests.Integration.EssayGrading;

public static class EssayGradingTestData
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public static async Task<(Guid EssayGradeId, Guid SessionId, Guid QuestionId)> SeedPendingAsync(ApiFactory factory, Guid studentId)
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
        var session = Session.StartQuiz(studentId, lesson, [mcq], isTestMode: false);
        context.Sessions.Add(session);
        var grade = EssayGrade.Request(studentId, session.Id, subject.Id, essay.Id, essay.Version, 5, "القصور الذاتي هو ممانعة الجسم لتغيير حالته الحركية.", DateTimeOffset.UtcNow.AddSeconds(-1));
        context.EssayGrades.Add(grade);
        await context.SaveChangesAsync(CancellationToken).ConfigureAwait(false);
        return (grade.Id, session.Id, essay.Id);
    }

    public static async Task GradeAsync(ApiFactory factory, Guid essayGradeId)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISender>().Send(new GradeEssayCommand(essayGradeId), CancellationToken).ConfigureAwait(false);
    }

    public static async Task<EssayGrade> ReadAsync(ApiFactory factory, Guid essayGradeId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.EssayGrades.AsNoTracking().SingleAsync(x => x.Id == essayGradeId, CancellationToken).ConfigureAwait(false);
    }
}
