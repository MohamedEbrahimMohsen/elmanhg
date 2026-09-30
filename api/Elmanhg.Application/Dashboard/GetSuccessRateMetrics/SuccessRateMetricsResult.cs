namespace Elmanhg.Application.Dashboard.GetSuccessRateMetrics;

public sealed record SuccessRateMetricsResult(DateOnly From, DateOnly To, Guid? SubjectId, int Attempts, int Correct, decimal? Rate, List<SuccessRateGroupResult> BySubject, List<SuccessRateGroupResult> ByUnit, List<SuccessRateGroupResult> ByLesson, DateTimeOffset GeneratedAt);
