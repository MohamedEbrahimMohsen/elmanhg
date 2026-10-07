using Core.Cache;
using Elmanhg.Application.Dashboard.Shared;
using MediatR;

namespace Elmanhg.Application.Dashboard.GetContentMetrics;

public sealed record GetContentMetricsQuery(Guid? SubjectId) : IRequest<ContentMetricsResult>, ICacheableQuery
{
    public string CacheKey => DashboardCacheKey.For("content", null, null, SubjectId);

    public string? CacheProfile => DashboardCacheKey.Profile;
}
