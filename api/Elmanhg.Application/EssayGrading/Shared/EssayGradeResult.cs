using Elmanhg.Application.GradeReviews.Shared;

namespace Elmanhg.Application.EssayGrading.Shared;

public sealed record EssayGradeResult(Guid Id, string Status, int MaxScore, DateTimeOffset RequestedAt, DateTimeOffset? GradedAt, decimal? Score, decimal? NormalisedScore, string? Outcome, string? Justification, IReadOnlyList<EssayCriterionResult> Criteria, GradeReviewNoteResult? Review);
