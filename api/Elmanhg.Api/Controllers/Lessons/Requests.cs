namespace Elmanhg.Api.Controllers.Lessons;

public sealed record CreateLessonRequest(Guid UnitId, string Name);

public sealed record LessonObjectiveRequest(Guid? Id, string Text);

public sealed record UpdateLessonRequest(string Name, string? Explanation, string? Summary, string? VideoUrl, List<LessonObjectiveRequest> Objectives);

public sealed record LessonPositionRequest(int Position);
