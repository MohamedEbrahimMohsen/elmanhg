using Elmanhg.Application.Dashboard.Shared;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Dashboard.Shared;

public sealed class DashboardRatesTests
{
    [Fact]
    public void Ratio_ZeroDenominator_ReturnsNull()
    {
        DashboardRates.Ratio(5, 0, DashboardRates.RateDecimals).Should().BeNull();
    }

    [Fact]
    public void Ratio_RoundsAwayFromZero()
    {
        DashboardRates.Ratio(1, 8, 2).Should().Be(0.13m);
    }

    [Fact]
    public void Seconds_Null_ReturnsNull()
    {
        DashboardRates.Seconds(null).Should().BeNull();
    }

    [Fact]
    public void Seconds_RoundsToWholeSeconds()
    {
        DashboardRates.Seconds(209.5).Should().Be(210);
    }
}
