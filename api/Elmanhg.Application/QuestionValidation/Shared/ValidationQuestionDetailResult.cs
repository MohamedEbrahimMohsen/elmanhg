using System.Text.Json;

namespace Elmanhg.Application.QuestionValidation.Shared;

public sealed record QuestionRevisionEntryResult(int Version, DateTimeOffset EditedAt);

public sealed record QuestionDecisionResult(int Version, string Outcome, string? Reason, string Difficulty, string? DifficultyChangedFrom, Guid DecidedBy, string? DecidedByName, DateTimeOffset DecidedAt);

public sealed record ValidationQuestionDetailResult(Guid Id, Guid SubjectId, string SubjectName, Guid UnitId, string UnitName, Guid LessonId, string LessonName, string LessonState, string Type, string Stem, JsonElement Body, JsonElement GradingSpec, string Explanation, string Difficulty, Guid? ObjectiveId, string? ObjectiveText, List<string> Tags, int MaxScore, int Version, string ValidationStatus, string? RejectionReason, DateTimeOffset SubmittedAt, DateTimeOffset? RetiredAt, List<QuestionRevisionEntryResult> Revisions, List<QuestionDecisionResult> Decisions);
