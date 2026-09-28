namespace Elmanhg.Domain.Mastery;

public sealed record LessonMasteryCount(Guid SubjectId, int SubjectOrder, Guid UnitId, int UnitOrder, Guid LessonId, int LessonOrder, int ServableCount, int MasteredCount, int SeenCount);
