namespace Elmanhg.Domain.Mastery;

public sealed record ObjectiveMasteryCount(Guid SubjectId, int SubjectOrder, Guid UnitId, int UnitOrder, Guid LessonId, int LessonOrder, Guid ObjectiveId, int ObjectiveOrder, int ServableCount, int MasteredCount, int SeenCount);
