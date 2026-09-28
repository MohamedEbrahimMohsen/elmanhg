using System.Text.Json.Nodes;

namespace Elmanhg.Domain.Questions;

public sealed record QuestionRevisionSnapshot(QuestionType Type, string Stem, JsonNode? Body, JsonNode? GradingSpec, string Explanation, int MaxScore);
