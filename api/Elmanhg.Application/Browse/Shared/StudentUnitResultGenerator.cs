using Elmanhg.Domain.Lessons;
using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;

namespace Elmanhg.Application.Browse.Shared;

public static class StudentUnitResultGenerator
{
    public static StudentUnitResult Generate(CurriculumUnit unit, Subject subject, IReadOnlyList<Lesson> lessons, IReadOnlyCollection<LessonMasteryCount> counts, IReadOnlyCollection<ExamBestScore> bests, IReadOnlySet<Guid> openLessonIds)
    {
        var countsByLesson = counts.ToDictionary(x => x.LessonId);
        var lessonResults = lessons
            .Select(lesson => GenerateLesson(lesson, countsByLesson, !openLessonIds.Contains(lesson.Id)))
            .ToList();
        var totals = MasteryTotals.Of(counts.Where(x => x.UnitId == unit.Id));
        return new StudentUnitResult(unit.Id, unit.Name, subject.Id, subject.Name, totals.ServableCount, totals.MasteredCount, totals.SeenCount, totals.MasteryPercent, UnitBestScore.Find(bests, unit.Id), lessonResults);
    }

    private static StudentLessonSummaryResult GenerateLesson(Lesson lesson, Dictionary<Guid, LessonMasteryCount> countsByLesson, bool isLocked)
    {
        var totals = countsByLesson.TryGetValue(lesson.Id, out var count) ? new MasteryTotals(count.ServableCount, count.MasteredCount, count.SeenCount) : new MasteryTotals(0, 0, 0);
        return new StudentLessonSummaryResult(lesson.Id, lesson.Name, totals.ServableCount, totals.MasteredCount, totals.SeenCount, totals.MasteryPercent, isLocked);
    }
}
