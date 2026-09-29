using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;

namespace Elmanhg.Application.Browse.Shared;

public static class StudentSubjectResultGenerator
{
    public static StudentSubjectResult Generate(Subject subject, IReadOnlyList<CurriculumUnit> units, IReadOnlyDictionary<Guid, int> lessonCounts, IReadOnlyCollection<LessonMasteryCount> counts, IReadOnlyCollection<ExamBestScore> bests)
    {
        var unitResults = units
            .Select(unit => GenerateUnit(unit, lessonCounts, counts, bests))
            .ToList();
        var totals = MasteryTotals.Of(counts);
        return new StudentSubjectResult(subject.Id, subject.Name, totals.ServableCount, totals.MasteredCount, totals.SeenCount, totals.MasteryPercent, unitResults);
    }

    private static StudentUnitSummaryResult GenerateUnit(CurriculumUnit unit, IReadOnlyDictionary<Guid, int> lessonCounts, IReadOnlyCollection<LessonMasteryCount> counts, IReadOnlyCollection<ExamBestScore> bests)
    {
        var totals = MasteryTotals.Of(counts.Where(x => x.UnitId == unit.Id));
        return new StudentUnitSummaryResult(unit.Id, unit.Name, lessonCounts.GetValueOrDefault(unit.Id), totals.ServableCount, totals.MasteredCount, totals.SeenCount, totals.MasteryPercent, UnitBestScore.Find(bests, unit.Id));
    }
}
