namespace Elmanhg.Domain.EssayGrading;

public sealed record EssayCriterionScore(string CriterionId, string Title, int Points, int MaxPoints, string Justification);
