namespace Elmanhg.Application.Shared.AiService;

public sealed record AiRubricCriterion(string Id, string Title, string? Description, int Points, IReadOnlyList<AiRubricLevel> Levels);
