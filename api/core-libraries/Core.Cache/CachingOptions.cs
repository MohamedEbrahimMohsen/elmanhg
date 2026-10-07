namespace Core.Cache;

public sealed class CachingOptions
{
    public TimeSpan DefaultTtl { get; set; } = TimeSpan.Zero;

    public Dictionary<string, TimeSpan> Profiles { get; } = new(StringComparer.Ordinal);

    public TimeSpan ResolveTtl(ICacheableQuery query) => query.Ttl ?? (query.CacheProfile is { } profile && Profiles.TryGetValue(profile, out var ttl) ? ttl : DefaultTtl);
}
