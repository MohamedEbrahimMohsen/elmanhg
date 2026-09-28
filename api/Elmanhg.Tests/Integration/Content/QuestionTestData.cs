using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Teachers;
using Elmanhg.Infrastructure.Data.Context;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elmanhg.Tests.Integration.Content;

public static class QuestionTestData
{
    public static async Task<Guid> SeedQuestionAsync(ApiFactory factory, Guid lessonId, bool approved, CancellationToken cancellationToken)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lesson = await context.Lessons.Include(x => x.Objectives).AsNoTracking().SingleAsync(x => x.Id == lessonId, cancellationToken).ConfigureAwait(false);
        var unit = await context.Units.AsNoTracking().SingleAsync(x => x.Id == lesson.UnitId, cancellationToken).ConfigureAwait(false);
        var subject = await context.Subjects.AsNoTracking().SingleAsync(x => x.Id == unit.SubjectId, cancellationToken).ConfigureAwait(false);
        var creator = Guid.NewGuid();
        var question = Question.Create(lesson, unit, QuestionType.Mcq, QuestionBuilder.McqContent(), new QuestionMetadata(QuestionDifficulty.Medium, null, []), creator);
        if (approved)
        {
            question.Approve(TeacherSubject.Create(User.CreateTeacher("Teacher", $"{Guid.NewGuid():N}@example.com"), subject, creator));
        }

        context.Questions.Add(question);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return question.Id;
    }

    public static async Task<Guid> SeedRetiredQuestionAsync(ApiFactory factory, Guid lessonId, CancellationToken cancellationToken)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lesson = await context.Lessons.Include(x => x.Objectives).AsNoTracking().SingleAsync(x => x.Id == lessonId, cancellationToken).ConfigureAwait(false);
        var unit = await context.Units.AsNoTracking().SingleAsync(x => x.Id == lesson.UnitId, cancellationToken).ConfigureAwait(false);
        var subject = await context.Subjects.AsNoTracking().SingleAsync(x => x.Id == unit.SubjectId, cancellationToken).ConfigureAwait(false);
        var creator = Guid.NewGuid();
        var question = Question.Create(lesson, unit, QuestionType.Mcq, QuestionBuilder.McqContent(), new QuestionMetadata(QuestionDifficulty.Medium, null, []), creator);
        question.Approve(TeacherSubject.Create(User.CreateTeacher("Teacher", $"{Guid.NewGuid():N}@example.com"), subject, creator));
        question.Retire(creator);

        context.Questions.Add(question);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return question.Id;
    }

    public static async Task<Guid> SeedRejectedQuestionAsync(ApiFactory factory, Guid lessonId, User teacher, string reason, CancellationToken cancellationToken)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lesson = await context.Lessons.Include(x => x.Objectives).AsNoTracking().SingleAsync(x => x.Id == lessonId, cancellationToken).ConfigureAwait(false);
        var unit = await context.Units.AsNoTracking().SingleAsync(x => x.Id == lesson.UnitId, cancellationToken).ConfigureAwait(false);
        var subject = await context.Subjects.AsNoTracking().SingleAsync(x => x.Id == unit.SubjectId, cancellationToken).ConfigureAwait(false);
        var creator = Guid.NewGuid();
        var question = Question.Create(lesson, unit, QuestionType.Mcq, QuestionBuilder.McqContent(), new QuestionMetadata(QuestionDifficulty.Medium, null, []), creator);
        question.Reject(TeacherSubject.Create(teacher, subject, creator), reason);

        context.Questions.Add(question);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return question.Id;
    }

    public static async Task<Question> ReadQuestionAsync(ApiFactory factory, Guid questionId, CancellationToken cancellationToken)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await context.Questions.Include(x => x.Revisions).AsNoTracking().SingleAsync(x => x.Id == questionId, cancellationToken).ConfigureAwait(false);
    }
}
