namespace Elmanhg.Application.Lessons.Shared;

public sealed record LessonResult(Guid Id, Guid UnitId, string Name, int Order, string State, int QuestionCount);
