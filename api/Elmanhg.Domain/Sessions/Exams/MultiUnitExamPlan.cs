using Core.Errors;
using Elmanhg.Domain.ExamBlueprints;
using Elmanhg.Domain.Questions;
using Elmanhg.Domain.SharedKernel.Exceptions;

namespace Elmanhg.Domain.Sessions.Exams;

public sealed record MultiUnitExamPlan(IReadOnlyList<MultiUnitExamUnitPlan> Units, int? TimeLimitMinutes, int PassMark)
{
    public List<ExamTypeCount> TypeCounts => Units
        .SelectMany(x => x.TypeCounts)
        .GroupBy(x => x.Type)
        .Select(x => new ExamTypeCount(x.Key, x.Sum(item => item.Count)))
        .Where(x => x.Count > 0)
        .OrderBy(x => x.Type)
        .ToList();

    public int QuestionCount => Units.Sum(x => x.QuestionCount);

    public void EnsureServable(IReadOnlyDictionary<QuestionType, int> servable)
    {
        var shortfalls = ExamBlueprintShortfall.Find(TypeCounts, servable);
        if (shortfalls.Count > 0)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.ExamShortfall, context: new Dictionary<string, object> { ["types"] = ExamBlueprintShortfall.Describe(shortfalls) });
        }
    }
}
