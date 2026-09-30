using Elmanhg.Application.GradeReviews.Shared;

namespace Elmanhg.Application.MathStepGrading.Shared;

public sealed record MathStepGradeResult(Guid Id, string Status, int MaxScore, DateTimeOffset RequestedAt, DateTimeOffset? GradedAt, decimal? Score, decimal? NormalisedScore, string? Outcome, string? FinalAnswerVerdict, string? Justification, IReadOnlyList<MathStepScoreResult> Steps, GradeReviewNoteResult? Review);
