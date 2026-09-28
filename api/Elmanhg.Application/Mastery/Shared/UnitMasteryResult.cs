namespace Elmanhg.Application.Mastery.Shared;

public sealed record UnitMasteryResult(Guid UnitId, string Name, int ServableCount, int MasteredCount, int SeenCount, int MasteryPercent, List<LessonMasteryResult> Lessons);
