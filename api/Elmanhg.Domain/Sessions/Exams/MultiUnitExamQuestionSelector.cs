using Elmanhg.Domain.ExamBlueprints;

namespace Elmanhg.Domain.Sessions.Exams;

public static class MultiUnitExamQuestionSelector
{
    public static List<Guid> Select(MultiUnitExamPlan plan, IReadOnlyDictionary<Guid, List<ExamCandidate>> candidatesByUnit, IReadOnlySet<Guid> masteredQuestionIds, Random random)
    {
        var all = candidatesByUnit.Values
            .SelectMany(x => x)
            .DistinctBy(x => x.QuestionId)
            .ToDictionary(x => x.QuestionId);
        List<Guid> selected = [];
        foreach (var unit in plan.Units)
        {
            var pool = candidatesByUnit.GetValueOrDefault(unit.UnitId) ?? [];
            var capped = unit.TypeCounts
                .Select(x => new ExamTypeCount(x.Type, Math.Min(x.Count, pool.Count(candidate => candidate.Type == x.Type))))
                .ToList();
            selected.AddRange(ExamQuestionSelector.Select(pool, masteredQuestionIds, capped, unit.DifficultyMix, random));
        }

        var missing = plan.TypeCounts
            .Select(x => new ExamTypeCount(x.Type, x.Count - selected.Count(id => all[id].Type == x.Type)))
            .Where(x => x.Count > 0)
            .ToList();
        if (missing.Count > 0)
        {
            var picked = selected.ToHashSet();
            var rest = all.Values
                .Where(x => !picked.Contains(x.QuestionId))
                .ToList();
            selected.AddRange(ExamQuestionSelector.Select(rest, masteredQuestionIds, missing, null, random));
        }

        return selected
            .OrderBy(x => all[x].Type)
            .ThenBy(x => all[x].Difficulty)
            .ToList();
    }
}
