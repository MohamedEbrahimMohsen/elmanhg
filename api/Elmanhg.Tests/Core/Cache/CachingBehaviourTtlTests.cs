using FluentAssertions;

namespace Elmanhg.Tests.Core.Cache;

public sealed class CachingBehaviourTtlTests : CachingBehaviourTestBase
{
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
}
