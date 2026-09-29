namespace Elmanhg.Application.TeacherThreads.Shared;

public sealed record TeacherThreadContextResult(Guid SubjectId, string SubjectName, Guid UnitId, string UnitName, Guid LessonId, string LessonName, Guid? QuestionId, int? QuestionVersion, string? QuestionStem, Guid? AttemptId);
