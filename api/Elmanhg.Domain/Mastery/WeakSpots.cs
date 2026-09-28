namespace Elmanhg.Domain.Mastery;

// PRD §7.6 / prototype vProgress: attempted but not fully mastered, lowest mastery first.
public static class WeakSpots
{
    public static List<LessonMasteryCount> PickLessons(IEnumerable<LessonMasteryCount> lessons, int count)
    {
        return lessons
            .Where(x => x.SeenCount > 0 && x.MasteredCount < x.ServableCount && x.ServableCount > 0)
            .OrderBy(x => (decimal)x.MasteredCount / x.ServableCount)
            .ThenBy(x => x.SubjectOrder)
            .ThenBy(x => x.UnitOrder)
            .ThenBy(x => x.LessonOrder)
            .ThenBy(x => x.LessonId)
            .Take(count)
            .ToList();
    }

    public static List<ObjectiveMasteryCount> PickObjectives(IEnumerable<ObjectiveMasteryCount> objectives, int count)
    {
        return objectives
            .Where(x => x.SeenCount > 0 && x.MasteredCount < x.ServableCount && x.ServableCount > 0)
            .OrderBy(x => (decimal)x.MasteredCount / x.ServableCount)
            .ThenBy(x => x.SubjectOrder)
            .ThenBy(x => x.UnitOrder)
            .ThenBy(x => x.LessonOrder)
            .ThenBy(x => x.ObjectiveOrder)
            .ThenBy(x => x.ObjectiveId)
            .Take(count)
            .ToList();
    }
}
