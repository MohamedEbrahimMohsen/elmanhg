namespace Elmanhg.Domain.EssayGrading;

public sealed record EssayAssessment(IReadOnlyList<EssayCriterionScore> Criteria, string Justification, decimal Confidence, string Model, string PromptVersion, int InputTokens, int OutputTokens, decimal CostUsd);
