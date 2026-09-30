using Elmanhg.Application.Dashboard.Shared;
using Elmanhg.Domain.SharedKernel;

namespace Elmanhg.Application.Dashboard.GetSolveRateMetrics;

public static class SolveRateMetricsResultGenerator
{
    public static SolveRateMetricsResult Generate(DashboardWindow window, Guid? subjectId, List<DailyTotal> attempts, List<DailyTotal> activeStudents)
    {
        var attemptsByDay = attempts.ToDictionary(x => x.Day, x => x.Value);
        var activeByDay = activeStudents.ToDictionary(x => x.Day, x => x.Value);
        var daily = window.Days
            .Select(x => new SolveRateDayResult(x, attemptsByDay.GetValueOrDefault(x), activeByDay.GetValueOrDefault(x)))
            .ToList();
        var totalAttempts = daily.Sum(x => x.Attempts);
        var activeStudentDays = daily.Sum(x => x.ActiveStudents);

        return new SolveRateMetricsResult(window.From, window.To, subjectId, totalAttempts, activeStudentDays, DashboardRates.Ratio(totalAttempts, activeStudentDays, DashboardRates.PerStudentDecimals), daily, window.Now);
    }
}
