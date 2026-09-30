namespace Elmanhg.Application.Shared.AiService;

public sealed record AiMathStepGradingResult(IReadOnlyList<AiMathStepScore> Steps, int TotalPoints, int MaxPoints, string Justification, decimal Confidence, string Model, string PromptVersion, int InputTokens, int OutputTokens, string? StopReason, decimal CostUsd);
