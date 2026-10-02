namespace Elmanhg.Application.TeacherThreads.Shared;

public sealed record TeacherReplyDeadlineResult(int ReplySlaHours, DateTimeOffset SlaDueAt, bool SkipsUncountedDays);
