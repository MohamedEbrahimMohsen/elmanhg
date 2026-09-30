using Elmanhg.Domain.Analytics;
using Elmanhg.Tests.Integration.Infrastructure;
using FluentAssertions;
using System.Text.Json;
using static Elmanhg.Tests.Integration.Dashboard.DashboardTestData;

namespace Elmanhg.Tests.Integration.Dashboard;

public sealed class FunnelMetricsEndpointTests(ApiFactory factory)
{
    private static readonly DateTimeOffset Landed = new(2021, 6, 6, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Answered = new(2021, 6, 9, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Get_SeededDay_CountsDistinctVisitorsAndMedian()
    {
        var visitorA = Guid.NewGuid();
        var visitorB = Guid.NewGuid();
        await SeedFunnelEventAsync(factory, visitorA, FunnelEventType.LandingViewed, Landed);
        await SeedFunnelEventAsync(factory, visitorA, FunnelEventType.LandingViewed, Landed.AddSeconds(10));
        await SeedFunnelEventAsync(factory, visitorA, FunnelEventType.SignUpStarted, Landed.AddSeconds(30));
        await SeedFunnelEventAsync(factory, visitorA, FunnelEventType.SignUpCompleted, Landed.AddSeconds(60));
        await SeedFunnelEventAsync(factory, visitorA, FunnelEventType.OnboardingCompleted, Landed.AddSeconds(90));
        await SeedFunnelEventAsync(factory, visitorA, FunnelEventType.FirstQuizAnswered, Landed.AddSeconds(120));
        await SeedFunnelEventAsync(factory, visitorB, FunnelEventType.LandingViewed, Landed);
        await SeedFunnelEventAsync(factory, visitorB, FunnelEventType.SignUpStarted, Landed.AddSeconds(60));
        await SeedFunnelEventAsync(factory, visitorB, FunnelEventType.FirstQuizAnswered, Landed.AddSeconds(300));
        await SeedFunnelEventAsync(factory, Guid.NewGuid(), FunnelEventType.LandingViewed, Landed);
        using var client = await AdminClientAsync(factory);

        var body = await GetJsonAsync(client, "funnel?from=2021-06-06&to=2021-06-06");

        body.GetProperty("steps").EnumerateArray().Select(x => x.GetProperty("visitors").GetInt32()).Should().Equal(3, 2, 1, 1, 2);
        body.GetProperty("completedJourneys").GetInt32().Should().Be(2);
        body.GetProperty("medianLandingToFirstAnswerSeconds").GetInt64().Should().Be(210);
    }

    [Fact]
    public async Task Get_AnswerWithoutLandingInRange_IsNotACompletedJourney()
    {
        var visitorD = Guid.NewGuid();
        var visitorE = Guid.NewGuid();
        await SeedFunnelEventAsync(factory, visitorD, FunnelEventType.LandingViewed, Answered.AddDays(-1));
        await SeedFunnelEventAsync(factory, visitorD, FunnelEventType.FirstQuizAnswered, Answered);
        await SeedFunnelEventAsync(factory, visitorE, FunnelEventType.FirstQuizAnswered, Answered);
        await SeedFunnelEventAsync(factory, visitorE, FunnelEventType.LandingViewed, Answered.AddSeconds(60));
        using var client = await AdminClientAsync(factory);

        var body = await GetJsonAsync(client, "funnel?from=2021-06-09&to=2021-06-09");

        body.GetProperty("steps").EnumerateArray().Select(x => x.GetProperty("visitors").GetInt32()).Should().Equal(1, 0, 0, 0, 2);
        body.GetProperty("completedJourneys").GetInt32().Should().Be(0);
        body.GetProperty("medianLandingToFirstAnswerSeconds").ValueKind.Should().Be(JsonValueKind.Null);
    }
}
