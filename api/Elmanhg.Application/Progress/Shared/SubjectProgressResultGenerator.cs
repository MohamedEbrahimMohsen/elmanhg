using Elmanhg.Domain.Mastery;
using Elmanhg.Domain.Sessions;
using Elmanhg.Domain.Subjects;
using Elmanhg.Domain.Units;

namespace Elmanhg.Application.Progress.Shared;

public static class SubjectProgressResultGenerator
{
    public static List<SubjectProgressResult> Generate(IReadOnlyList<Subject> subjects, IReadOnlyList<CurriculumUnit> units, IReadOnlyCollection<LessonMasteryCount> counts, IReadOnlyCollection<UnitExamBestScore> bests)
    {
        var bestByKey = bests.ToDictionary(x => x.ScopeKey, x => x.BestScorePercent);
        return subjects
            .Select(subject => GenerateSubject(subject, units, counts, bestByKey))
            .ToList();
    }

    private static SubjectProgressResult GenerateSubject(Subject subject, IReadOnlyList<CurriculumUnit> units, IReadOnlyCollection<LessonMasteryCount> counts, Dictionary<string, decimal> bestByKey)
    {
        var unitResults = units
            .Where(x => x.SubjectId == subject.Id)
            .Select(unit => GenerateUnit(unit, counts, bestByKey))
            .ToList();
        var totals = MasteryTotals.Of(counts.Where(x => x.SubjectId == subject.Id));
        return new SubjectProgressResult(subject.Id, subject.Name, totals.ServableCount, totals.MasteredCount, totals.SeenCount, totals.MasteryPercent, unitResults);
    }

    private static UnitProgressResult GenerateUnit(CurriculumUnit unit, IReadOnlyCollection<LessonMasteryCount> counts, Dictionary<string, decimal> bestByKey)
    {
        var totals = MasteryTotals.Of(counts.Where(x => x.UnitId == unit.Id));
        decimal? best = bestByKey.TryGetValue(new UnitExamScope(unit.Id).ToKey(), out var score) ? score : null;
        return new UnitProgressResult(unit.Id, unit.Name, totals.ServableCount, totals.MasteredCount, totals.SeenCount, totals.MasteryPercent, best);
    }
}
