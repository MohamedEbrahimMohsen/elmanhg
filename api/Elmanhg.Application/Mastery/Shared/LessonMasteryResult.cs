namespace Elmanhg.Application.Mastery.Shared;

public sealed record LessonMasteryResult(Guid LessonId, string Name, int ServableCount, int MasteredCount, int SeenCount, int MasteryPercent);
