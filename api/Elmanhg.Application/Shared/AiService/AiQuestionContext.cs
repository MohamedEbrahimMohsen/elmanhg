namespace Elmanhg.Application.Shared.AiService;

public sealed record AiQuestionContext(Guid Id, string Stem, string? StudentAnswer, string? CorrectAnswer, string? Explanation);
