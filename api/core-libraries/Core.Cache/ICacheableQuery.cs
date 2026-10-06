namespace Core.Cache;

public interface ICacheableQuery
{
    string CacheKey { get; }

    TimeSpan? Ttl => null;
}
