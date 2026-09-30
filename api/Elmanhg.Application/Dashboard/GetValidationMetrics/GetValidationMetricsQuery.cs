using Elmanhg.Application.Dashboard.Shared;
using MediatR;

namespace Elmanhg.Application.Dashboard.GetValidationMetrics;

public sealed record GetValidationMetricsQuery(DateOnly? From, DateOnly? To, Guid? SubjectId) : IRequest<ValidationMetricsResult>, IDashboardRangeQuery
{
    public string CacheKey => DashboardCacheKey.For("validation", From, To, SubjectId);
}
