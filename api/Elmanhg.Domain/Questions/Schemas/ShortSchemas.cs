namespace Elmanhg.Domain.Questions.Schemas;

public enum ShortAnswerKind { Numeric, Text }

public enum ToleranceMode { Absolute, Percent }

public sealed record ShortBody(ShortAnswerKind? AnswerKind);

public sealed record ShortGradingSpec(decimal? Value, decimal? Tolerance, ToleranceMode? ToleranceMode, List<string>? AcceptedAnswers, bool? UnifyLetterVariants);
