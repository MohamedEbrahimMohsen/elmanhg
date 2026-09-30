namespace Elmanhg.Application.MathStepGrading.Shared;

public sealed record MathStepScoreResult(int StepIndex, string Step, int Points, int MaxPoints, string Justification);
