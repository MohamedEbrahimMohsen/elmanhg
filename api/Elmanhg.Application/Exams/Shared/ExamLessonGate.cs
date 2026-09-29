using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Lessons;

namespace Elmanhg.Application.Exams.Shared;

public static class ExamLessonGate
{
    public static async Task<int> CountUnopenedAsync(Guid studentId, IReadOnlyCollection<Guid> unitIds, ILessonRepository lessonRepository, ILessonOpeningRepository lessonOpeningRepository, CancellationToken cancellationToken)
    {
        var lessons = await lessonRepository.FindAsync(x => unitIds.Contains(x.UnitId) && x.State == LessonState.Published, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var lessonIds = lessons.Select(x => x.Id).ToList();
        var openings = await lessonOpeningRepository.FindAsync(x => x.StudentId == studentId && lessonIds.Contains(x.LessonId), cancellationToken, asNoTracking: true).ConfigureAwait(false);
        return lessonIds.Count - openings.Select(x => x.LessonId).Distinct().Count();
    }

    public static async Task EnsureOpenedAsync(Guid studentId, IReadOnlyCollection<Guid> unitIds, ILessonRepository lessonRepository, ILessonOpeningRepository lessonOpeningRepository, CancellationToken cancellationToken)
    {
        if (await CountUnopenedAsync(studentId, unitIds, lessonRepository, lessonOpeningRepository, cancellationToken).ConfigureAwait(false) > 0)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.ExamLessonsNotOpened);
        }
    }
}
