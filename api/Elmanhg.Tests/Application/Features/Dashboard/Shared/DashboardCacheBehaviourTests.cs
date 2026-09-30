using Elmanhg.Application.Dashboard.Shared;
using Elmanhg.Application.Shared.Options;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.Dashboard.Shared;

public sealed record CachedProbeQuery(string Key) : IRequest<string>, IDashboardQuery
{
    public string CacheKey => Key;
}

public sealed record PlainProbeRequest : IRequest<string>;

public sealed class DashboardCacheBehaviourTests : IDisposable
{
    private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());
    private readonly DashboardOptions _options = new();
    private int _nextCalls;

    [Fact]
    public async Task Handle_ColdCache_CallsNextAndCachesResult()
    {
        var result = await Behaviour<CachedProbeQuery>().Handle(new CachedProbeQuery("a"), Next, TestContext.Current.CancellationToken);

        _nextCalls.Should().Be(1);
        _memoryCache.Get<string>("a").Should().Be(result);
    }

    [Fact]
    public async Task Handle_WarmCache_ReturnsCachedWithoutCallingNext()
    {
        var behaviour = Behaviour<CachedProbeQuery>();
        var first = await behaviour.Handle(new CachedProbeQuery("a"), Next, TestContext.Current.CancellationToken);

        var second = await behaviour.Handle(new CachedProbeQuery("a"), Next, TestContext.Current.CancellationToken);

        second.Should().Be(first);
        _nextCalls.Should().Be(1);
    }

    [Fact]
    public async Task Handle_DifferentCacheKey_CallsNextAgain()
    {
        var behaviour = Behaviour<CachedProbeQuery>();
        await behaviour.Handle(new CachedProbeQuery("a"), Next, TestContext.Current.CancellationToken);

        await behaviour.Handle(new CachedProbeQuery("b"), Next, TestContext.Current.CancellationToken);

        _nextCalls.Should().Be(2);
    }

    [Fact]
    public async Task Handle_CacheSecondsZero_AlwaysCallsNext()
    {
        _options.CacheSeconds = 0;
        var behaviour = Behaviour<CachedProbeQuery>();
        await behaviour.Handle(new CachedProbeQuery("a"), Next, TestContext.Current.CancellationToken);

        await behaviour.Handle(new CachedProbeQuery("a"), Next, TestContext.Current.CancellationToken);

        _nextCalls.Should().Be(2);
        _memoryCache.Count.Should().Be(0);
    }

    [Fact]
    public async Task Handle_NonDashboardRequest_PassesThroughUncached()
    {
        var behaviour = Behaviour<PlainProbeRequest>();
        await behaviour.Handle(new PlainProbeRequest(), Next, TestContext.Current.CancellationToken);

        await behaviour.Handle(new PlainProbeRequest(), Next, TestContext.Current.CancellationToken);

        _nextCalls.Should().Be(2);
        _memoryCache.Count.Should().Be(0);
    }

    public void Dispose() => _memoryCache.Dispose();

    private DashboardCacheBehaviour<TRequest, string> Behaviour<TRequest>() where TRequest : notnull => new(_memoryCache, Options.Create(_options));

    private Task<string> Next(CancellationToken cancellationToken)
    {
        _nextCalls++;
        return Task.FromResult($"result-{_nextCalls}");
    }
}
