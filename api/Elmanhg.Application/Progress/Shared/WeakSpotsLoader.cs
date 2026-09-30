using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Subjects;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Application.Progress.Shared;

public static class WeakSpotsLoader
{
    public static async Task<WeakSpotsResult> LoadAsync(IQuestionMasteryRepository questionMasteryRepository, ILessonRepository lessonRepository, ISubjectRepository subjectRepository, ProgressOptions options, Guid studentId, CancellationToken cancellationToken)
    {
        var lessonCounts = await questionMasteryRepository.GetLessonCountsAsync(studentId, null, cancellationToken).ConfigureAwait(false);
        var objectiveCounts = await questionMasteryRepository.GetObjectiveCountsAsync(studentId, cancellationToken).ConfigureAwait(false);
        var weakLessons = WeakSpots.PickLessons(lessonCounts, options.WeakLessonCount);
        var weakObjectives = WeakSpots.PickObjectives(objectiveCounts, options.WeakObjectiveCount);
        var lessonIds = weakLessons
            .Select(x => x.LessonId)
            .Union(weakObjectives.Select(x => x.LessonId))
            .ToList();
        if (lessonIds.Count == 0)
        {
            return new WeakSpotsResult([], []);
        }

        var lessons = await lessonRepository.FindAsync(x => lessonIds.Contains(x.Id), cancellationToken, include: query => query.Include(x => x.Objectives), asNoTracking: true).ConfigureAwait(false);
        var subjectIds = weakLessons
            .Select(x => x.SubjectId)
            .Union(weakObjectives.Select(x => x.SubjectId))
            .ToList();
        var subjects = await subjectRepository.FindAsync(x => subjectIds.Contains(x.Id), cancellationToken, asNoTracking: true).ConfigureAwait(false);
        return WeakSpotsResultGenerator.Generate(weakLessons, weakObjectives, lessons, subjects);
    }
}
