using Elmanhg.Application.EssayGrading.Shared;
using Elmanhg.Application.MathStepGrading.Shared;
using System.Text.Json;

namespace Elmanhg.Application.GradeReviews.Shared;

public sealed record GradeReviewDetailResult(Guid Id, string Kind, Guid SubjectId, Guid QuestionId, int QuestionVersion, string QuestionType, string Stem, JsonElement Body, JsonElement GradingSpec, string UnitName, string LessonName, JsonElement Answer, int MaxScore, string Status, string ReviewReason, DateTimeOffset RequestedAt, decimal? AiScore, decimal? Confidence, string? Justification, IReadOnlyList<EssayCriterionResult> Criteria, IReadOnlyList<MathStepScoreResult> Steps, string? FinalAnswerVerdict, decimal? FinalScore, GradeReviewNoteResult? Review);
