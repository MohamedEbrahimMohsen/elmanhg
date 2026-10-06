using Core.Utilities.Time;
using FluentAssertions;

namespace Elmanhg.Tests.Core.Utilities;

public sealed class TimeZoneInfoExtensionsTests
{
    private static readonly TimeZoneInfo Cairo = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");

    [Fact]
    public void LocalDate_AfterLocalMidnightBeforeUtcMidnight_ReturnsLocalDay()
    {
        var localDate = Cairo.LocalDate(new DateTimeOffset(2026, 1, 14, 22, 30, 0, TimeSpan.Zero));

        localDate.Should().Be(new DateOnly(2026, 1, 15));
    }

    [Fact]
    public void StartOfDay_OrdinaryDay_ReturnsLocalMidnightInUtc()
    {
        var start = Cairo.StartOfDay(new DateOnly(2026, 1, 15));

        start.Should().Be(new DateTimeOffset(2026, 1, 14, 22, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void StartOfDay_SpringForwardMidnight_ReturnsTransitionInstant()
    {
        var start = Cairo.StartOfDay(new DateOnly(2026, 4, 24));

        start.Should().Be(new DateTimeOffset(2026, 4, 23, 22, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void StartOfDay_Result_HasZeroOffset()
    {
        var start = Cairo.StartOfDay(new DateOnly(2026, 7, 1));

        start.Offset.Should().Be(TimeSpan.Zero);
    }
}
