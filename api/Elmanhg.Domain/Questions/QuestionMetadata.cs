namespace Elmanhg.Domain.Questions;

public sealed record QuestionMetadata(QuestionDifficulty Difficulty, Guid? ObjectiveId, IReadOnlyList<string> Tags);
