using Elmanhg.Domain.Questions.Schemas;
using System.Text.Json;

namespace Elmanhg.Domain.ExamBlueprints;

public static class ExamBlueprintJson
{
    public static string SerializeTypeCounts(IEnumerable<ExamTypeCount> typeCounts)
    {
        var canonical = typeCounts
            .Where(x => x.Count > 0)
            .OrderBy(x => x.Type)
            .ToList();
        return JsonSerializer.Serialize(canonical, QuestionJson.SerializerOptions);
    }

    public static List<ExamTypeCount> DeserializeTypeCounts(string json) => JsonSerializer.Deserialize<List<ExamTypeCount>>(json, QuestionJson.SerializerOptions) ?? throw new InvalidOperationException("Exam blueprint type counts are empty.");

    public static string SerializeDifficultyMix(ExamDifficultyMix mix) => JsonSerializer.Serialize(mix, QuestionJson.SerializerOptions);

    public static ExamDifficultyMix DeserializeDifficultyMix(string json) => JsonSerializer.Deserialize<ExamDifficultyMix>(json, QuestionJson.SerializerOptions) ?? throw new InvalidOperationException("Exam blueprint difficulty mix is empty.");
}
