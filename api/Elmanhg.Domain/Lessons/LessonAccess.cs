namespace Elmanhg.Domain.Lessons;

public static class LessonAccess
{
    // PRD §11.1: Free opens the first N published lessons of each unit; curriculum order as LessonSequence.
    public static HashSet<Guid> OpenLessonIds(IEnumerable<LessonPosition> publishedLessons, int? openLessonsPerUnit)
    {
        if (openLessonsPerUnit is null)
        {
            return publishedLessons
                .Select(x => x.Id)
                .ToHashSet();
        }

        return publishedLessons
            .GroupBy(x => x.UnitId)
            .SelectMany(x => x
                .OrderBy(lesson => lesson.Order)
                .ThenBy(lesson => lesson.CreationDate)
                .ThenBy(lesson => lesson.Id)
                .Take(openLessonsPerUnit.Value))
            .Select(x => x.Id)
            .ToHashSet();
    }

    public static bool IsOpen(Guid lessonId, IEnumerable<LessonPosition> publishedLessons, int? openLessonsPerUnit) => OpenLessonIds(publishedLessons, openLessonsPerUnit).Contains(lessonId);
}
