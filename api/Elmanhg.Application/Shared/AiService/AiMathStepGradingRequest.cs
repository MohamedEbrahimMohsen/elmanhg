namespace Elmanhg.Application.Shared.AiService;

public sealed record AiMathStepGradingRequest(string Question, IReadOnlyList<string> ModelSolution, IReadOnlyList<string> AcceptedAnswers, IReadOnlyList<string> Steps, string FinalAnswer, string? Subject, IReadOnlyList<string> Objectives);
