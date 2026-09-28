namespace Elmanhg.Application.Progress.Shared;

public sealed record WeakObjectiveResult(Guid ObjectiveId, string Text, Guid LessonId, string LessonName, Guid SubjectId, string SubjectName, int ServableCount, int MasteredCount, int SeenCount, int MasteryPercent);
