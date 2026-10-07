using Core.Cache;
using FluentAssertions;
using MediatR;

namespace Elmanhg.Tests.Core.Cache;

public sealed record ProfileProbeQuery(string? Profile, TimeSpan? QueryTtl = null) : IRequest<string>, ICacheableQuery
{
    public string CacheKey => "probe";

    public TimeSpan? Ttl => QueryTtl;

    public string? CacheProfile => Profile;
}

public sealed class CachingOptionsTests
{
    private readonly CachingOptions _options = new() { DefaultTtl = TimeSpan.FromSeconds(60) };

    [Fact]
    public void ResolveTtl_QueryTtl_WinsOverProfileAndDefault()
    {
        _options.Profiles["p"] = TimeSpan.FromSeconds(30);

        var ttl = _options.ResolveTtl(new ProfileProbeQuery("p", TimeSpan.FromSeconds(5)));

        ttl.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ResolveTtl_ConfiguredProfile_UsesProfileTtl()
    {
        _options.Profiles["p"] = TimeSpan.FromSeconds(30);

        var ttl = _options.ResolveTtl(new ProfileProbeQuery("p"));

        ttl.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void ResolveTtl_UnknownProfile_UsesDefaultTtl()
    {
        _options.Profiles["p"] = TimeSpan.FromSeconds(30);

        var ttl = _options.ResolveTtl(new ProfileProbeQuery("other"));

        ttl.Should().Be(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public void ResolveTtl_NoProfileNoTtl_UsesDefaultTtl()
    {
        _options.Profiles["p"] = TimeSpan.FromSeconds(30);

        var ttl = _options.ResolveTtl(new ProfileProbeQuery(null));

        ttl.Should().Be(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public void ResolveTtl_ProfileZero_ReturnsZeroEvenWithPositiveDefault()
    {
        _options.Profiles["p"] = TimeSpan.Zero;

        var ttl = _options.ResolveTtl(new ProfileProbeQuery("p"));

        ttl.Should().Be(TimeSpan.Zero);
    }
}
