using Core.Cache;

namespace Elmanhg.Application.Dashboard.Shared;

public interface IDashboardRangeQuery : ICacheableQuery, IDashboardRange
{
    string? ICacheableQuery.CacheProfile => DashboardCacheKey.Profile;
}
