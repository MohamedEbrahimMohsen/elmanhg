namespace Elmanhg.Domain.Questions.Schemas;

public sealed record FillBlank(string? Id);

public sealed record FillBody(List<FillBlank>? Blanks);

public sealed record FillBlankAnswers(string? Id, List<string>? AcceptedAnswers);

public sealed record FillGradingSpec(List<FillBlankAnswers>? Blanks, AnswerNormalization? Normalization = null);
