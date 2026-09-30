using Elmanhg.Application.Dashboard.Shared;
using Elmanhg.Domain.Analytics;

namespace Elmanhg.Application.Dashboard.GetFunnelMetrics;

public static class FunnelMetricsResultGenerator
{
    public static FunnelMetricsResult Generate(DashboardWindow window, List<FunnelStepCount> steps, FunnelTiming timing)
    {
        var visitors = Enum.GetValues<FunnelEventType>()
            .Select(type => (Type: type, Visitors: steps.FirstOrDefault(x => x.Type == type)?.Visitors ?? 0))
            .ToList();
        var results = visitors
            .Select((step, index) => new FunnelStepResult(step.Type, step.Visitors, index == 0 ? null : DashboardRates.Ratio(step.Visitors, visitors[index - 1].Visitors, DashboardRates.RateDecimals)))
            .ToList();

        return new FunnelMetricsResult(window.From, window.To, results, timing.Completed, DashboardRates.Seconds(timing.MedianSeconds), window.Now);
    }
}
