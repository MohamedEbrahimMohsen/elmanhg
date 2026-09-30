namespace Elmanhg.Domain.Questions;

public sealed record QuestionPlacement(Guid QuestionId, Guid SubjectId, Guid UnitId, Guid LessonId);
