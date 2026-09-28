using Elmanhg.Domain.Questions.Schemas;
using System.Text.Json;

namespace Elmanhg.Domain.Sessions;

public sealed record MultiUnitExamScope(Guid SubjectId, IReadOnlyList<Guid> UnitIds, int Size)
{
    public string ToKey() => $"units:{Size}:{string.Join(",", UnitIds.Order().Select(x => x.ToString("D")))}";

    public string ToJson() => JsonSerializer.Serialize(this, QuestionJson.SerializerOptions);

    public static MultiUnitExamScope FromJson(string json) => JsonSerializer.Deserialize<MultiUnitExamScope>(json, QuestionJson.SerializerOptions) ?? throw new InvalidOperationException("Multi-unit exam scope is empty.");
}
