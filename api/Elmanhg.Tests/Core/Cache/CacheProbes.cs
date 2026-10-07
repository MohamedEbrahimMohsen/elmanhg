using Core.Cache;
using MediatR;

namespace Elmanhg.Tests.Core.Cache;

public sealed record CacheProbeQuery(string Key, TimeSpan? QueryTtl = null) : IRequest<string>, ICacheableQuery
{
    public string CacheKey => Key;

    public TimeSpan? Ttl => QueryTtl;
}

public sealed record UncachedProbeRequest : IRequest<string>;
