using Core.Cache;
using Elmanhg.Application;
using Elmanhg.Application.Dashboard.GetAskTeacherMetrics;
using Elmanhg.Application.Dashboard.GetContentMetrics;
using Elmanhg.Application.Dashboard.GetFunnelMetrics;
using Elmanhg.Application.Dashboard.GetMyTeacherStats;
using Elmanhg.Application.Dashboard.GetPaymentMetrics;
using Elmanhg.Application.Dashboard.GetSolveRateMetrics;
using Elmanhg.Application.Dashboard.GetStudentMetrics;
using Elmanhg.Application.Dashboard.GetSubscriberMetrics;
using Elmanhg.Application.Dashboard.GetSuccessRateMetrics;
using Elmanhg.Application.Dashboard.GetValidationMetrics;
using Elmanhg.Application.Dashboard.Shared;
using Elmanhg.Application.Shared.Options;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.Dashboard.Shared;

public sealed class DashboardCachingTests
{
    public static TheoryData<object> DashboardCardQueries =>
    [
        new GetAskTeacherMetricsQuery(null, null, null),
        new GetContentMetricsQuery(null),
        new GetFunnelMetricsQuery(null, null),
        new GetPaymentMetricsQuery(null, null),
        new GetSolveRateMetricsQuery(null, null, null),
        new GetStudentMetricsQuery(null, null),
        new GetSubscriberMetricsQuery(null, null),
        new GetSuccessRateMetricsQuery(null, null, null),
        new GetValidationMetricsQuery(null, null, null),
    ];

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

    [Theory]
    [MemberData(nameof(DashboardCardQueries))]
    public void DashboardCardQuery_IsCacheableWithoutOwnTtl(object query)
    {
        var cacheable = query is ICacheableQuery { Ttl: null };

        cacheable.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(DashboardCardQueries))]
    public void DashboardCardQuery_UsesDashboardCacheProfile(object query)
    {
        var profile = ((ICacheableQuery)query).CacheProfile;

        profile.Should().Be(DashboardCacheKey.Profile);
    }

    [Fact]
    public void GetMyTeacherStatsQuery_IsNotCacheable()
    {
        object query = new GetMyTeacherStatsQuery(null, null);

        query.Should().NotBeAssignableTo<ICacheableQuery>();
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
