using Core.Cache;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Core.Cache;

public abstract class CachingBehaviourTestBase : IDisposable
{
    protected readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());
    protected readonly CachingOptions _options = new() { DefaultTtl = TimeSpan.FromSeconds(60) };
    protected int _nextCalls;

    public void Dispose() => _memoryCache.Dispose();

    protected CachingBehaviour<TRequest, string> Behaviour<TRequest>() where TRequest : notnull => new(_memoryCache, Options.Create(_options));

    protected Task<string> Next(CancellationToken cancellationToken)
    {
        _nextCalls++;
        return Task.FromResult($"result-{_nextCalls}");
    }
}
