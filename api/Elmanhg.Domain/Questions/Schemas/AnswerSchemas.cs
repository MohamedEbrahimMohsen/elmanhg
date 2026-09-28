namespace Elmanhg.Domain.Questions.Schemas;

public sealed record McqAnswer(string? OptionId);

public sealed record MultiAnswer(List<string>? OptionIds);

public sealed record TrueFalseAnswer(bool? Value);

public sealed record FillBlankResponse(string? Id, string? Text);

public sealed record FillAnswer(List<FillBlankResponse>? Blanks);

public sealed record ShortAnswer(string? Text);
