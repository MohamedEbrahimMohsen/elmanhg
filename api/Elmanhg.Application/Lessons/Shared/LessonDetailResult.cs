namespace Elmanhg.Application.Lessons.Shared;

public sealed record LessonDetailResult(Guid Id, Guid UnitId, string Name, int Order, string State, string Explanation, string Summary, string? VideoUrl, List<LessonObjectiveResult> Objectives);
