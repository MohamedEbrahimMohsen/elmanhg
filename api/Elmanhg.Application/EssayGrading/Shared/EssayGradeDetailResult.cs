namespace Elmanhg.Application.EssayGrading.Shared;

public sealed record EssayGradeDetailResult(IReadOnlyList<EssayCriterionResult> Criteria, string Justification, decimal Confidence, string Model, string PromptVersion, decimal CostUsd);
