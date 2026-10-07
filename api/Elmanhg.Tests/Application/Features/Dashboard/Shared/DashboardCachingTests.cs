using Core.Cache;
using Elmanhg.Application;
using Elmanhg.Application.Dashboard.GetFunnelMetrics;
using Elmanhg.Application.Shared.Options;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.Dashboard.Shared;

public sealed class DashboardCachingTests
{
    [Fact]
    public void AddApplication_DashboardCacheSeconds_SetsDashboardQueryTtl()
    {
        using var provider = BuildProvider(new Dictionary<string, string?> { ["Dashboard:CacheSeconds"] = "45" });

        var options = provider.GetRequiredService<IOptions<CachingOptions>>().Value;

        options.ResolveTtl(new GetFunnelMetricsQuery(null, null)).Should().Be(TimeSpan.FromSeconds(45));
    }

    [Fact]
    public void AddApplication_NoDashboardConfiguration_UsesSixtySecondDefault()
    {
        using var provider = BuildProvider([]);

        var options = provider.GetRequiredService<IOptions<CachingOptions>>().Value;

        options.ResolveTtl(new GetFunnelMetricsQuery(null, null)).Should().Be(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public void AddApplication_DashboardCacheSecondsZero_DisablesDashboardCaching()
    {
        using var provider = BuildProvider(new Dictionary<string, string?> { ["Dashboard:CacheSeconds"] = "0" });

        var options = provider.GetRequiredService<IOptions<CachingOptions>>().Value;

        options.ResolveTtl(new GetFunnelMetricsQuery(null, null)).Should().Be(TimeSpan.Zero);
        options.DefaultTtl.Should().Be(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public void AddApplication_CachingDefaultSeconds_SetsDefaultTtlOnly()
    {
        using var provider = BuildProvider(new Dictionary<string, string?> { ["Caching:DefaultSeconds"] = "15" });

        var options = provider.GetRequiredService<IOptions<CachingOptions>>().Value;

        options.DefaultTtl.Should().Be(TimeSpan.FromSeconds(15));
        options.ResolveTtl(new GetFunnelMetricsQuery(null, null)).Should().Be(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public void AddApplication_NoCachingConfiguration_DefaultTtlIsSixtySeconds()
    {
        using var provider = BuildProvider([]);

        var options = provider.GetRequiredService<IOptions<CachingOptions>>().Value;

        options.DefaultTtl.Should().Be(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public void AddApplication_CachingDefaultSecondsOutOfRange_FailsValidation()
    {
        using var provider = BuildProvider(new Dictionary<string, string?> { ["Caching:DefaultSeconds"] = "3601" });

        var act = () => provider.GetRequiredService<IOptions<QueryCachingOptions>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    private static ServiceProvider BuildProvider(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddApplication();
        return services.BuildServiceProvider();
    }
}
