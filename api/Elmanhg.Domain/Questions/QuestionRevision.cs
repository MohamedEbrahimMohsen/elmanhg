using Core.DDD.Entities;
using Elmanhg.Domain.Questions.Schemas;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Elmanhg.Domain.Questions;

public class QuestionRevision : Entity
{
    public Guid QuestionId { get; private set; }
    public int Version { get; private set; }
    public string Snapshot { get; private set; } = "{}";
    public Guid EditedBy { get; private set; }
    public DateTimeOffset EditedAt { get; private set; }

    private QuestionRevision(Guid id) : base(id) { }

    internal static QuestionRevision Create(Question question, Guid editedBy)
    {
        var snapshot = new QuestionRevisionSnapshot(question.Type, question.Stem, JsonNode.Parse(question.Body), JsonNode.Parse(question.GradingSpec), question.Explanation, question.MaxScore);
        return new QuestionRevision(Guid.NewGuid())
        {
            QuestionId = question.Id,
            Version = question.Version,
            Snapshot = JsonSerializer.Serialize(snapshot, QuestionJson.SerializerOptions),
            EditedBy = editedBy,
            EditedAt = DateTimeOffset.UtcNow,
        };
    }
}
