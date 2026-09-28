namespace Elmanhg.Domain.Questions.Schemas;

public sealed record ChoiceOption(string? Id, string? Text);

public sealed record ChoiceBody(List<ChoiceOption>? Options);

public sealed record McqGradingSpec(string? CorrectOptionId);

public sealed record MultiGradingSpec(List<string>? CorrectOptionIds, bool PartialCredit = false);
