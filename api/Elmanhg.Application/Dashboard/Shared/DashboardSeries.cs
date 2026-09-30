using Elmanhg.Domain.SharedKernel;

namespace Elmanhg.Application.Dashboard.Shared;

public static class DashboardSeries
{
    public static List<DailyValueResult> Fill(DashboardWindow window, IEnumerable<DailyTotal> totals)
    {
        var byDay = totals.ToDictionary(x => x.Day, x => x.Value);
        return window.Days
            .Select(x => new DailyValueResult(x, byDay.GetValueOrDefault(x)))
            .ToList();
    }
}
