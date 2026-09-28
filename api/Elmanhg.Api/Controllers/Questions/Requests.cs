using Elmanhg.Domain.Questions;
using System.Text.Json;

namespace Elmanhg.Api.Controllers.Questions;

public sealed record CreateQuestionRequest(Guid LessonId, QuestionType? Type, string? Stem, JsonElement Body, JsonElement GradingSpec, string? Explanation, QuestionDifficulty? Difficulty, Guid? ObjectiveId, List<string>? Tags, int? MaxScore);

public sealed record UpdateQuestionRequest(QuestionType? Type, string? Stem, JsonElement Body, JsonElement GradingSpec, string? Explanation, QuestionDifficulty? Difficulty, Guid? ObjectiveId, List<string>? Tags, int? MaxScore);
