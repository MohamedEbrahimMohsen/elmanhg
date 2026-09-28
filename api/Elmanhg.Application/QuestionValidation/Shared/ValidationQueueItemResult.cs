namespace Elmanhg.Application.QuestionValidation.Shared;

public sealed record ValidationQueueItemResult(Guid Id, Guid SubjectId, Guid UnitId, string UnitName, Guid LessonId, string LessonName, string Type, string Stem, string Difficulty, int Version, DateTimeOffset SubmittedAt, bool OpenedInSession);
