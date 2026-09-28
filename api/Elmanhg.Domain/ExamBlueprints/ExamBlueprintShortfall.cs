using Elmanhg.Domain.Questions;

namespace Elmanhg.Domain.ExamBlueprints;

public static class ExamBlueprintShortfall
{
    public static List<ExamTypeShortfall> Find(IEnumerable<ExamTypeCount> required, IReadOnlyDictionary<QuestionType, int> available)
    {
        return required
            .Where(x => x.Count > available.GetValueOrDefault(x.Type))
            .Select(x => new ExamTypeShortfall(x.Type, x.Count, available.GetValueOrDefault(x.Type)))
            .OrderBy(x => x.Type)
            .ToList();
    }
}
