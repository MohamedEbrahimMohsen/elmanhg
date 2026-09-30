namespace Elmanhg.Application.Shared.AiService;

public sealed record AiEssayGradingRequest(string Question, IReadOnlyList<AiRubricCriterion> Criteria, IReadOnlyList<string> ModelAnswers, string Essay, string? Subject, IReadOnlyList<string> Objectives);
