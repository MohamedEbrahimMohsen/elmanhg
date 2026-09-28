using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Questions;

namespace Elmanhg.Domain.Sessions.Exams;

public static class MultiUnitBlueprintMerge
{
    // Exam scores are out of 100 (PRD §7.4).
    private const int PassMarkMax = 100;

    public static MultiUnitExamPlan Merge(IReadOnlyList<MultiUnitExamPart> parts, int size, int maxTimeLimitMinutes)
    {
        if (parts.Count < MultiUnitExamSizes.MinUnits)
        {
            throw new InvalidOperationException("A multi-unit exam needs at least two units.");
        }

        if (!MultiUnitExamSizes.IsAllowed(size))
        {
            throw new InvalidOperationException("The multi-unit exam size is not allowed.");
        }

        var total = parts.Sum(x => x.Blueprint.QuestionCount);
        var cells = parts
            .SelectMany((part, unitIndex) => part.Blueprint.GetTypeCounts()
                .Where(x => x.Count > 0)
                .Select(x => new Cell(unitIndex, x.Type, x.Count * size / total, x.Count * size % total)))
            .ToList();
        var receivers = cells
            .OrderByDescending(x => x.Remainder)
            .ThenBy(x => x.UnitIndex)
            .ThenBy(x => x.Type)
            .Take(size - cells.Sum(x => x.Allocated))
            .ToHashSet();
        var units = parts
            .Select((part, unitIndex) => new MultiUnitExamUnitPlan(part.UnitId, part.Blueprint.IsSubjectDefault, UnitTypeCounts(cells, receivers, unitIndex), part.Blueprint.GetDifficultyMix()))
            .ToList();
        return new MultiUnitExamPlan(units, TimeLimit(parts, units, maxTimeLimitMinutes), PassMark(parts, units, size));
    }

    private static List<ExamTypeCount> UnitTypeCounts(List<Cell> cells, HashSet<Cell> receivers, int unitIndex)
    {
        return cells
            .Where(x => x.UnitIndex == unitIndex)
            .Select(x => new ExamTypeCount(x.Type, x.Allocated + (receivers.Contains(x) ? 1 : 0)))
            .Where(x => x.Count > 0)
            .OrderBy(x => x.Type)
            .ToList();
    }

    private static int? TimeLimit(IReadOnlyList<MultiUnitExamPart> parts, List<MultiUnitExamUnitPlan> units, int maxTimeLimitMinutes)
    {
        var contributing = parts
            .Zip(units)
            .Where(x => x.Second.QuestionCount > 0)
            .ToList();
        if (contributing.Any(x => x.First.Blueprint.TimeLimitMinutes is null))
        {
            return null;
        }

        var minutes = contributing.Sum(x => (decimal)x.First.Blueprint.TimeLimitMinutes.GetValueOrDefault() * x.Second.QuestionCount / x.First.Blueprint.QuestionCount);
        return (int)Math.Clamp(Math.Ceiling(minutes), 1, maxTimeLimitMinutes);
    }

    private static int PassMark(IReadOnlyList<MultiUnitExamPart> parts, List<MultiUnitExamUnitPlan> units, int size)
    {
        var weighted = parts
            .Zip(units)
            .Where(x => x.Second.QuestionCount > 0)
            .Sum(x => (decimal)x.First.Blueprint.PassMark * x.Second.QuestionCount / size);
        return (int)Math.Clamp(Math.Round(weighted, 0, MidpointRounding.AwayFromZero), 1, PassMarkMax);
    }

    private sealed record Cell(int UnitIndex, QuestionType Type, int Allocated, int Remainder);
}
