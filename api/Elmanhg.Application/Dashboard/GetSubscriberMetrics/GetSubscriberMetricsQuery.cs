using Elmanhg.Application.Dashboard.Shared;
using MediatR;

namespace Elmanhg.Application.Dashboard.GetSubscriberMetrics;

public sealed record GetSubscriberMetricsQuery(DateOnly? From, DateOnly? To) : IRequest<SubscriberMetricsResult>, IDashboardRangeQuery
{
    public string CacheKey => DashboardCacheKey.For("subscribers", From, To, null);
}
