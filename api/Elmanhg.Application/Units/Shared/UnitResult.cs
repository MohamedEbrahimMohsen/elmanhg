namespace Elmanhg.Application.Units.Shared;

public sealed record UnitResult(Guid Id, Guid SubjectId, string Name, int Order, int LessonCount);
