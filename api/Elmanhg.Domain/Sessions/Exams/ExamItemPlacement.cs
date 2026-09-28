namespace Elmanhg.Domain.Sessions.Exams;

public sealed record ExamItemPlacement(Guid QuestionId, Guid LessonId, int LessonOrder, Guid? ObjectiveId, int ObjectiveOrder);
