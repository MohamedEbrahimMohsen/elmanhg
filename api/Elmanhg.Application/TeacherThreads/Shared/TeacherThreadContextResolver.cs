using Core.DDD.Repositories;
using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.TeacherThreads;

namespace Elmanhg.Application.TeacherThreads.Shared;

public static partial class TeacherThreadContextResolver
{
    public static async Task<TeacherThreadContext> ResolveAsync(Guid? lessonId, Guid? questionId, Guid? attemptId, Guid studentId, TeacherThreadContextSources sources, CancellationToken cancellationToken)
    {
        var part = attemptId is { } attempt
            ? await ResolveAttemptAsync(attempt, studentId, sources, cancellationToken).ConfigureAwait(false)
            : questionId is { } question ? await ResolveQuestionAsync(question, sources, cancellationToken).ConfigureAwait(false) : null;
        var lesson = await sources.Lessons.GetByIdAsync(part?.Question.LessonId ?? lessonId ?? Guid.Empty, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        if (part is { AttemptId: null } && (lesson is null || !ServableQuestionSpecification.IsSatisfiedBy(part.Question, lesson)))
        {
            throw new NotFoundCoreException(ErrorCodes.QuestionNotFound);
        }

        if (lesson is null || lesson.State != LessonState.Published)
        {
            throw new NotFoundCoreException(ErrorCodes.LessonNotFound);
        }

        var unit = await sources.Units.GetRequiredAsync(lesson.UnitId, ErrorCodes.LessonNotFound, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var subject = await sources.Subjects.GetRequiredAsync(unit.SubjectId, ErrorCodes.LessonNotFound, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        return new TeacherThreadContext(subject.Id, subject.Name, unit.Id, unit.Name, lesson.Id, lesson.Name, part?.Question.Id, part?.Version, part?.Stem, part?.AttemptId);
    }
}
