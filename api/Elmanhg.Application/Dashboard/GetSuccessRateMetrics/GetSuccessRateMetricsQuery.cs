using Elmanhg.Application.Dashboard.Shared;
using MediatR;

namespace Elmanhg.Application.Dashboard.GetSuccessRateMetrics;

public sealed record GetSuccessRateMetricsQuery(DateOnly? From, DateOnly? To, Guid? SubjectId) : IRequest<SuccessRateMetricsResult>, IDashboardRangeQuery
{
    public string CacheKey => DashboardCacheKey.For("success-rate", From, To, SubjectId);
}
