namespace Elmanhg.Domain.MathStepGrading;

public sealed record MathStepScore(int StepIndex, string Step, int Points, int MaxPoints, string Justification);
