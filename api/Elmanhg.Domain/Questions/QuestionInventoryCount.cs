namespace Elmanhg.Domain.Questions;

public sealed record QuestionInventoryCount(QuestionValidationStatus Status, QuestionType Type, bool IsRetired, int Count);
