using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.Sessions.Exams;

namespace Elmanhg.Application.Exams.Shared;

public static class MultiUnitExamPreviewResultGenerator
{
    public static MultiUnitExamPreviewResult Generate(MultiUnitExamUnits selection, MultiUnitExamPlan plan, int size, IReadOnlyDictionary<QuestionType, int> available)
    {
        var mergedCounts = plan.TypeCounts;
        var typeCounts = mergedCounts
            .Select(x => new ExamTypeAvailabilityResult(x.Type, x.Count, available.GetValueOrDefault(x.Type)))
            .ToList();
        var blueprint = new ExamBlueprintSummaryResult(plan.Units.All(x => x.IsSubjectDefault), plan.QuestionCount, typeCounts, null, plan.TimeLimitMinutes, plan.PassMark);
        var isAvailable = ExamBlueprintShortfall.Find(mergedCounts, available).Count == 0;
        var units = plan.Units
            .Select(x => new MultiUnitExamUnitShareResult(x.UnitId, selection.Units.First(unit => unit.Id == x.UnitId).Name, x.QuestionCount, x.IsSubjectDefault))
            .ToList();
        return new MultiUnitExamPreviewResult(selection.Subject.Id, size, blueprint, isAvailable, units);
    }
}
