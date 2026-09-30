namespace Elmanhg.Application.Dashboard.GetSolveRateMetrics;

public sealed record SolveRateMetricsResult(DateOnly From, DateOnly To, Guid? SubjectId, long Attempts, long ActiveStudentDays, decimal? AttemptsPerActiveStudentPerDay, List<SolveRateDayResult> Daily, DateTimeOffset GeneratedAt);
