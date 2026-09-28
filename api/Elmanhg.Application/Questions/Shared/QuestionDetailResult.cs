using System.Text.Json;

namespace Elmanhg.Application.Questions.Shared;

public sealed record QuestionDetailResult(Guid Id, Guid LessonId, Guid SubjectId, string Type, string Stem, JsonElement Body, JsonElement GradingSpec, string Explanation, string Difficulty, Guid? ObjectiveId, List<string> Tags, int MaxScore, int Version, string ValidationStatus, string? RejectionReason);
