using Elmanhg.Application.Dashboard.Shared;
using Elmanhg.Application.Shared.Options;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Dashboard.Shared;

public sealed class DashboardWindowTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
    private readonly DashboardOptions _options = new();

    [Fact]
    public void Resolve_NoDates_EndsTodayAndSpansDefaultDays()
    {
        var window = DashboardWindow.Resolve(null, null, Now, _options);

        window.From.Should().Be(new DateOnly(2025, 12, 17));
        window.To.Should().Be(new DateOnly(2026, 1, 15));
        window.DayCount.Should().Be(30);
    }

    [Fact]
    public void Resolve_OnlyFrom_EndsToday()
    {
        var window = DashboardWindow.Resolve(new DateOnly(2026, 1, 1), null, Now, _options);

        window.From.Should().Be(new DateOnly(2026, 1, 1));
        window.To.Should().Be(new DateOnly(2026, 1, 15));
    }

    [Fact]
    public void Resolve_OnlyTo_StartsDefaultDaysBeforeTo()
    {
        var to = new DateOnly(2025, 6, 30);

        var window = DashboardWindow.Resolve(null, to, Now, _options);

        window.From.Should().Be(to.AddDays(-29));
        window.To.Should().Be(to);
    }

    [Fact]
    public void Resolve_WinterDay_StartAndEndAreCairoMidnightsInUtc()
    {
        var day = new DateOnly(2026, 1, 15);

        var window = DashboardWindow.Resolve(day, day, Now, _options);

        window.Start.Should().Be(new DateTimeOffset(2026, 1, 14, 22, 0, 0, TimeSpan.Zero));
        window.End.Should().Be(new DateTimeOffset(2026, 1, 15, 22, 0, 0, TimeSpan.Zero));
        window.Start.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void LocalDay_LateUtcEvening_IsNextCairoDay()
    {
        var day = DashboardWindow.LocalDay(new DateTimeOffset(2026, 1, 14, 23, 0, 0, TimeSpan.Zero), _options.TimeZone);

        day.Should().Be(new DateOnly(2026, 1, 15));
    }

    [Fact]
    public void StartOfDay_CairoSpringForwardDay_DoesNotThrow()
    {
        var window = DashboardWindow.Resolve(null, null, Now, _options);

        var springForward = window.StartOfDay(new DateOnly(2026, 4, 24));
        var nextDay = window.StartOfDay(new DateOnly(2026, 4, 25));

        springForward.Should().BeBefore(nextDay);
        springForward.Should().Be(new DateTimeOffset(2026, 4, 23, 22, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Days_EnumeratesEveryDayInclusive()
    {
        var window = DashboardWindow.Resolve(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 3), Now, _options);

        window.Days.Should().Equal(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 3));
    }
}
