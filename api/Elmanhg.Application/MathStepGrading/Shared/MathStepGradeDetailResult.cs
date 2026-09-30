namespace Elmanhg.Application.MathStepGrading.Shared;

public sealed record MathStepGradeDetailResult(IReadOnlyList<MathStepScoreResult> Steps, string FinalAnswerVerdict, string Justification, decimal Confidence, string Model, string PromptVersion, decimal CostUsd);
