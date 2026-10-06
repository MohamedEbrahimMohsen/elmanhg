using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Core.Cache;

public sealed class CachingBehaviour<TRequest, TResponse>(IMemoryCache memoryCache, IOptions<CachingOptions> cachingOptions) : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not ICacheableQuery query)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        var ttl = query.Ttl ?? cachingOptions.Value.DefaultTtl;
        if (ttl <= TimeSpan.Zero)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        if (memoryCache.TryGetValue(query.CacheKey, out TResponse? cached) && cached is not null)
        {
            return cached;
        }

        var result = await next(cancellationToken).ConfigureAwait(false);
        memoryCache.Set(query.CacheKey, result, ttl);
        return result;
    }
}
