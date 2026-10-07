using Core.Cache;
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
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Dashboard.Shared;

public sealed class DashboardCardQueryCachingTests
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
}
