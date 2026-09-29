namespace Elmanhg.Application.Shared.AiService;

public sealed record AiLessonContext(Guid Id, string Name, string Explanation, IReadOnlyList<string> Objectives, string Summary);
