using Elmanhg.Application.Dashboard.Shared;
using MediatR;

namespace Elmanhg.Application.Dashboard.GetFunnelMetrics;

public sealed record GetFunnelMetricsQuery(DateOnly? From, DateOnly? To) : IRequest<FunnelMetricsResult>, IDashboardRangeQuery
{
    public string CacheKey => DashboardCacheKey.For("funnel", From, To, null);
}
