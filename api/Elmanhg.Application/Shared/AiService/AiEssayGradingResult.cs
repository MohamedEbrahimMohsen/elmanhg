namespace Elmanhg.Application.Shared.AiService;

public sealed record AiEssayGradingResult(IReadOnlyList<AiEssayCriterionScore> Criteria, int TotalPoints, int MaxPoints, string Justification, decimal Confidence, string Model, string PromptVersion, int InputTokens, int OutputTokens, string? StopReason, decimal CostUsd);
