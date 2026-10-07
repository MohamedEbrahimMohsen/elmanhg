using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;

namespace Elmanhg.Tests.Core.Cache;

public sealed class CachingBehaviourTests : CachingBehaviourTestBase
{
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
    public async Task Handle_NonCacheableRequest_PassesThroughUncached()
    {
        var behaviour = Behaviour<UncachedProbeRequest>();
        await behaviour.Handle(new UncachedProbeRequest(), Next, TestContext.Current.CancellationToken);

        await behaviour.Handle(new UncachedProbeRequest(), Next, TestContext.Current.CancellationToken);

        _nextCalls.Should().Be(2);
        _memoryCache.Count.Should().Be(0);
    }
}
