namespace Elmanhg.Application.Dashboard.GetSolveRateMetrics;

public sealed record SolveRateDayResult(DateOnly Date, long Attempts, long ActiveStudents);
