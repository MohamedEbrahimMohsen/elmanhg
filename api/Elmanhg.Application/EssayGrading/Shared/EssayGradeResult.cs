namespace Elmanhg.Application.EssayGrading.Shared;

public sealed record EssayGradeResult(Guid Id, string Status, int MaxScore, DateTimeOffset RequestedAt, DateTimeOffset? GradedAt, decimal? Score, decimal? NormalisedScore, string? Outcome, string? Justification, IReadOnlyList<EssayCriterionResult> Criteria);
