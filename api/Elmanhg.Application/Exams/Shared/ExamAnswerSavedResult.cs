namespace Elmanhg.Application.Exams.Shared;

public sealed record ExamAnswerSavedResult(Guid QuestionId, DateTimeOffset AnswerSavedAt);
