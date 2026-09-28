namespace Elmanhg.Application.Exams.Shared;

public sealed record ExamSessionResult(Guid Id, string Kind, bool IsTestMode, string? SubjectName, List<ExamUnitResult> Units, DateTimeOffset StartedAt, int? TimeLimitMinutes, DateTimeOffset? Deadline, DateTimeOffset ServerNow, int PassMark, DateTimeOffset? SubmittedAt, decimal? ScorePercent, bool? IsPassed, long ElapsedMilliseconds, List<ExamItemResult> Items, List<ExamLessonResult> Lessons, List<ExamObjectiveResult> WeakestObjectives);

public sealed record ExamUnitResult(Guid UnitId, string? Name);
