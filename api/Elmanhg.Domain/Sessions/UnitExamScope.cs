using Elmanhg.Domain.Questions.Schemas;
using System.Text.Json;

namespace Elmanhg.Domain.Sessions;

public sealed record UnitExamScope(Guid UnitId)
{
    public string ToKey() => $"unit:{UnitId:D}";

    public string ToJson() => JsonSerializer.Serialize(this, QuestionJson.SerializerOptions);

    public static UnitExamScope FromJson(string json) => JsonSerializer.Deserialize<UnitExamScope>(json, QuestionJson.SerializerOptions) ?? throw new InvalidOperationException("Unit exam scope is empty.");
}
