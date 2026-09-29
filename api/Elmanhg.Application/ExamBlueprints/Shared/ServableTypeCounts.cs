using Elmanhg.Domain.Questions;

namespace Elmanhg.Application.ExamBlueprints.Shared;

public static class ServableTypeCounts
{
    public static Dictionary<QuestionType, int> ForSubject(IEnumerable<ServableQuestionCount> counts)
    {
        return counts
            .GroupBy(x => x.Type)
            .ToDictionary(x => x.Key, x => x.Sum(item => item.Count));
    }

    public static Dictionary<QuestionType, int> ForUnit(IEnumerable<ServableQuestionCount> counts, Guid unitId)
    {
        return counts
            .Where(x => x.UnitId == unitId)
            .GroupBy(x => x.Type)
            .ToDictionary(x => x.Key, x => x.Sum(item => item.Count));
    }

    public static List<ExamTypeCountResult> ToResults(IReadOnlyDictionary<QuestionType, int> available)
    {
        return ServableQuestionSpecification.ServedTypes
            .Select(x => new ExamTypeCountResult(x, available.GetValueOrDefault(x)))
            .ToList();
    }
}
