using Elmanhg.Application.Dashboard.Shared;
using MediatR;

namespace Elmanhg.Application.Dashboard.GetSolveRateMetrics;

public sealed record GetSolveRateMetricsQuery(DateOnly? From, DateOnly? To, Guid? SubjectId) : IRequest<SolveRateMetricsResult>, IDashboardRangeQuery
{
    public string CacheKey => DashboardCacheKey.For("solve-rate", From, To, SubjectId);
}
