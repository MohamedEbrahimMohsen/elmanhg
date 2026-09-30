namespace Elmanhg.Domain.Sessions;

public sealed record LessonAttemptOutcome(Guid SubjectId, Guid UnitId, Guid LessonId, int Attempts, int Correct);
