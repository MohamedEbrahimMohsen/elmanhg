namespace Elmanhg.Domain.Questions.Schemas;

public enum MathAnswerForm { Equivalent, Simplified, Factored, Expanded, Exact }

public sealed record MathStepsBody;

public sealed record MathStepsGradingSpec(List<string>? AcceptedAnswers, MathAnswerForm? Form, decimal? Tolerance, ToleranceMode? ToleranceMode);

public sealed record MathStepsAnswer(List<string?>? Steps, string? FinalAnswer);
