using Elmanhg.Application.Dashboard.Shared;
using MediatR;

namespace Elmanhg.Application.Dashboard.GetPaymentMetrics;

public sealed record GetPaymentMetricsQuery(DateOnly? From, DateOnly? To) : IRequest<PaymentMetricsResult>, IDashboardRangeQuery
{
    public string CacheKey => DashboardCacheKey.For("payments", From, To, null);
}
