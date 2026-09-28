using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;

namespace Elmanhg.Application.Mastery.Shared;

public static class SubjectMasteryResultGenerator
{
    public static SubjectMasteryDetailResult Generate(Subject subject, IReadOnlyList<CurriculumUnit> units, IReadOnlyList<Lesson> lessons, IReadOnlyCollection<LessonMasteryCount> counts)
    {
        var countsByLesson = counts.ToDictionary(x => x.LessonId);
        var unitResults = units
            .Select(unit => GenerateUnit(unit, lessons, counts, countsByLesson))
            .ToList();
        var totals = MasteryTotals.Of(counts);
        return new SubjectMasteryDetailResult(subject.Id, subject.Name, totals.ServableCount, totals.MasteredCount, totals.SeenCount, totals.MasteryPercent, unitResults);
    }

    private static UnitMasteryResult GenerateUnit(CurriculumUnit unit, IReadOnlyList<Lesson> lessons, IReadOnlyCollection<LessonMasteryCount> counts, Dictionary<Guid, LessonMasteryCount> countsByLesson)
    {
        var lessonResults = lessons
            .Where(x => x.UnitId == unit.Id)
            .Select(lesson => GenerateLesson(lesson, countsByLesson))
            .ToList();
        var totals = MasteryTotals.Of(counts.Where(x => x.UnitId == unit.Id));
        return new UnitMasteryResult(unit.Id, unit.Name, totals.ServableCount, totals.MasteredCount, totals.SeenCount, totals.MasteryPercent, lessonResults);
    }

    private static LessonMasteryResult GenerateLesson(Lesson lesson, Dictionary<Guid, LessonMasteryCount> countsByLesson)
    {
        var totals = countsByLesson.TryGetValue(lesson.Id, out var count) ? new MasteryTotals(count.ServableCount, count.MasteredCount, count.SeenCount) : new MasteryTotals(0, 0, 0);
        return new LessonMasteryResult(lesson.Id, lesson.Name, totals.ServableCount, totals.MasteredCount, totals.SeenCount, totals.MasteryPercent);
    }
}
