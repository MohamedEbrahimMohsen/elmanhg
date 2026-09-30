using Elmanhg.Application.Dashboard.Shared;

namespace Elmanhg.Application.Dashboard.GetStudentMetrics;

public sealed record StudentMetricsResult(DateOnly From, DateOnly To, int Total, int NewInRange, int NewThisWeek, int ActiveToday, int ActiveThisMonth, List<DailyValueResult> DailyActive, DateTimeOffset GeneratedAt);
