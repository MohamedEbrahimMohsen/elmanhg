using Core.Cache;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Core.Cache;

public sealed record CacheProbeQuery(string Key, TimeSpan? QueryTtl = null) : IRequest<string>, ICacheableQuery
{
    public string CacheKey => Key;

    public TimeSpan? Ttl => QueryTtl;
}

public sealed record UncachedProbeRequest : IRequest<string>;

public sealed class CachingBehaviourTests : IDisposable
{
    private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());
    private readonly CachingOptions _options = new() { DefaultTtl = TimeSpan.FromSeconds(60) };
    private int _nextCalls;

    [Fact]
    public async Task Handle_ColdCache_CallsNextAndCachesResult()
    {
        var result = await Behaviour<CacheProbeQuery>().Handle(new CacheProbeQuery("a"), Next, TestContext.Current.CancellationToken);

        _nextCalls.Should().Be(1);
        _memoryCache.Get<string>("a").Should().Be(result);
    }

    [Fact]
    public async Task Handle_WarmCache_ReturnsCachedWithoutCallingNext()
    {
        var behaviour = Behaviour<CacheProbeQuery>();
        var first = await behaviour.Handle(new CacheProbeQuery("a"), Next, TestContext.Current.CancellationToken);

        var second = await behaviour.Handle(new CacheProbeQuery("a"), Next, TestContext.Current.CancellationToken);

        second.Should().Be(first);
        _nextCalls.Should().Be(1);
    }

    [Fact]
    public async Task Handle_DifferentCacheKey_CallsNextAgain()
    {
        var behaviour = Behaviour<CacheProbeQuery>();
        await behaviour.Handle(new CacheProbeQuery("a"), Next, TestContext.Current.CancellationToken);

        await behaviour.Handle(new CacheProbeQuery("b"), Next, TestContext.Current.CancellationToken);

        _nextCalls.Should().Be(2);
    }

    [Fact]
    public async Task Handle_DefaultTtlZero_AlwaysCallsNext()
    {
        _options.DefaultTtl = TimeSpan.Zero;
        var behaviour = Behaviour<CacheProbeQuery>();
        await behaviour.Handle(new CacheProbeQuery("a"), Next, TestContext.Current.CancellationToken);

        await behaviour.Handle(new CacheProbeQuery("a"), Next, TestContext.Current.CancellationToken);

        _nextCalls.Should().Be(2);
        _memoryCache.Count.Should().Be(0);
    }

    [Fact]
    public async Task Handle_NonCacheableRequest_PassesThroughUncached()
    {
        var behaviour = Behaviour<UncachedProbeRequest>();
        await behaviour.Handle(new UncachedProbeRequest(), Next, TestContext.Current.CancellationToken);

        await behaviour.Handle(new UncachedProbeRequest(), Next, TestContext.Current.CancellationToken);

        _nextCalls.Should().Be(2);
        _memoryCache.Count.Should().Be(0);
    }

    [Fact]
    public async Task Handle_QueryTtlSet_OverridesZeroDefault()
    {
        _options.DefaultTtl = TimeSpan.Zero;
        var behaviour = Behaviour<CacheProbeQuery>();
        await behaviour.Handle(new CacheProbeQuery("a", TimeSpan.FromMinutes(1)), Next, TestContext.Current.CancellationToken);

        await behaviour.Handle(new CacheProbeQuery("a", TimeSpan.FromMinutes(1)), Next, TestContext.Current.CancellationToken);

        _nextCalls.Should().Be(1);
    }

    [Fact]
    public async Task Handle_QueryTtlZero_IsNotCached()
    {
        var behaviour = Behaviour<CacheProbeQuery>();
        await behaviour.Handle(new CacheProbeQuery("a", TimeSpan.Zero), Next, TestContext.Current.CancellationToken);

        await behaviour.Handle(new CacheProbeQuery("a", TimeSpan.Zero), Next, TestContext.Current.CancellationToken);

        _nextCalls.Should().Be(2);
        _memoryCache.Count.Should().Be(0);
    }

    public void Dispose() => _memoryCache.Dispose();

    private CachingBehaviour<TRequest, string> Behaviour<TRequest>() where TRequest : notnull => new(_memoryCache, Options.Create(_options));

    private Task<string> Next(CancellationToken cancellationToken)
    {
        _nextCalls++;
        return Task.FromResult($"result-{_nextCalls}");
    }
}
