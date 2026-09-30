using Elmanhg.Application.Shared.Options;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Dashboard.Shared;

public sealed class DashboardCacheBehaviour<TRequest, TResponse>(IMemoryCache memoryCache, IOptions<DashboardOptions> dashboardOptions) : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var options = dashboardOptions.Value;
        if (request is not IDashboardQuery query || options.CacheSeconds == 0)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        if (memoryCache.TryGetValue(query.CacheKey, out TResponse? cached) && cached is not null)
        {
            return cached;
        }

        var result = await next(cancellationToken).ConfigureAwait(false);
        memoryCache.Set(query.CacheKey, result, TimeSpan.FromSeconds(options.CacheSeconds));
        return result;
    }
}
