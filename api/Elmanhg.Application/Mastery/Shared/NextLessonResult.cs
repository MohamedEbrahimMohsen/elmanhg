namespace Elmanhg.Application.Mastery.Shared;

public sealed record NextLessonResult(Guid LessonId, string LessonName, Guid SubjectId, string SubjectName, int MasteryPercent);
