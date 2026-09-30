namespace Elmanhg.Domain.MathStepGrading;

public sealed record MathStepAssessment(IReadOnlyList<MathStepScore> Steps, string Justification, decimal Confidence, string Model, string PromptVersion, int InputTokens, int OutputTokens, decimal CostUsd);
