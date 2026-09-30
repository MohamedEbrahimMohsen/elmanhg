namespace Elmanhg.Application.EssayGrading.Shared;

public sealed record EssayCriterionResult(string CriterionId, string Title, int Points, int MaxPoints, string Justification);
