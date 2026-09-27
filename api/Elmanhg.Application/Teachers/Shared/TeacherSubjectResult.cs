namespace Elmanhg.Application.Teachers.Shared;

public sealed record TeacherSubjectResult(Guid TeacherId, Guid SubjectId, DateTimeOffset AssignedAt);
