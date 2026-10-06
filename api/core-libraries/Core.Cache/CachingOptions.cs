namespace Core.Cache;

public sealed class CachingOptions
{
    public TimeSpan DefaultTtl { get; set; } = TimeSpan.Zero;
}
