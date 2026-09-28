namespace Elmanhg.Domain.Questions;

public sealed record ServableQuestionCount(Guid UnitId, QuestionType Type, int Count);
