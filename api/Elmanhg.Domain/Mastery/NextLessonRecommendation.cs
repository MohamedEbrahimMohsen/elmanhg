namespace Elmanhg.Domain.Mastery;

// PRD §7.3 / prototype vStudentHome: lowest mastery first, curriculum order breaks ties.
public static class NextLessonRecommendation
{
    public static LessonMasteryCount? Pick(IEnumerable<LessonMasteryCount> lessons)
    {
        return lessons
            .Where(x => x.ServableCount > 0 && x.MasteredCount < x.ServableCount)
            .OrderBy(x => (decimal)x.MasteredCount / x.ServableCount)
            .ThenBy(x => x.SubjectOrder)
            .ThenBy(x => x.UnitOrder)
            .ThenBy(x => x.LessonOrder)
            .ThenBy(x => x.LessonId)
            .FirstOrDefault();
    }
}
