using Elmanhg.Domain.Questions;
using System.Text.Json;

namespace Elmanhg.Application.Questions.Shared;

public sealed record QuestionFields(QuestionType? Type, string? Stem, JsonElement Body, JsonElement GradingSpec, string? Explanation, QuestionDifficulty? Difficulty, Guid? ObjectiveId, IList<string> Tags, int? MaxScore);
