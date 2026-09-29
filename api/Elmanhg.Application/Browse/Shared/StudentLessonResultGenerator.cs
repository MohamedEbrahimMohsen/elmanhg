using Elmanhg.Application.Lessons.Shared;
using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;

namespace Elmanhg.Application.Browse.Shared;

public static class StudentLessonResultGenerator
{
    public static StudentLessonResult Generate(Lesson lesson, CurriculumUnit unit, Subject subject, IReadOnlyList<CurriculumUnit> units, IReadOnlyList<Lesson> publishedLessons, IReadOnlyCollection<LessonMasteryCount> counts)
    {
        var ordered = LessonSequence.Order(units, publishedLessons);
        var neighbours = LessonSequence.Neighbours(ordered, lesson.Id);
        var objectives = lesson.Objectives
            .OrderBy(x => x.Order)
            .Select(x => new LessonObjectiveResult(x.Id, x.Text, x.Order))
            .ToList();
        var count = counts.FirstOrDefault(x => x.LessonId == lesson.Id);
        var totals = count is null ? new MasteryTotals(0, 0, 0) : new MasteryTotals(count.ServableCount, count.MasteredCount, count.SeenCount);
        return new StudentLessonResult(lesson.Id, lesson.Name, unit.Id, unit.Name, subject.Id, subject.Name, lesson.Explanation, lesson.Summary, lesson.VideoUrl, objectives, totals.ServableCount, totals.MasteredCount, totals.SeenCount, totals.MasteryPercent, Link(neighbours.Previous, units), Link(neighbours.Next, units));
    }

    private static LessonLinkResult? Link(Lesson? lesson, IReadOnlyList<CurriculumUnit> units) => lesson is null ? null : new LessonLinkResult(lesson.Id, lesson.Name, lesson.UnitId, units.First(u => u.Id == lesson.UnitId).Name);
}
