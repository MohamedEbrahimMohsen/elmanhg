namespace Elmanhg.Domain.Questions.Schemas;

public sealed record EssayBody(int? MaxWords);

public sealed record RubricLevel(int? Points, string? Description);

public sealed record RubricCriterion(string? Id, string? Title, string? Description, int? Points, List<RubricLevel>? Levels);

public sealed record EssayGradingSpec(List<RubricCriterion>? Criteria, List<string>? ModelAnswers);
