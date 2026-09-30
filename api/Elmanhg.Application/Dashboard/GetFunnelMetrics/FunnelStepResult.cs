using Elmanhg.Domain.Analytics;

namespace Elmanhg.Application.Dashboard.GetFunnelMetrics;

public sealed record FunnelStepResult(FunnelEventType Type, int Visitors, decimal? ConversionFromPrevious);
