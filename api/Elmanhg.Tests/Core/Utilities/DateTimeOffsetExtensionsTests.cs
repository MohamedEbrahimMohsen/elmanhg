using Core.Utilities.Time;
using FluentAssertions;

namespace Elmanhg.Tests.Core.Utilities;

public sealed class DateTimeOffsetExtensionsTests
{
    private static readonly DateTimeOffset Whole = new(2026, 2, 3, 10, 20, 30, 456, 789, TimeSpan.Zero);

    [Fact]
    public void TruncateToMicroseconds_SubMicrosecondTicks_DropsThem()
    {
        var truncated = Whole.AddTicks(7).TruncateToMicroseconds();

        truncated.Ticks.Should().Be(Whole.Ticks);
        (truncated.Ticks % TimeSpan.TicksPerMicrosecond).Should().Be(0);
    }

    [Fact]
    public void TruncateToMicroseconds_NonUtcOffset_KeepsOffset()
    {
        var local = new DateTimeOffset(Whole.DateTime, TimeSpan.FromHours(3)).AddTicks(3);

        var truncated = local.TruncateToMicroseconds();

        truncated.Offset.Should().Be(TimeSpan.FromHours(3));
        truncated.DateTime.Should().Be(Whole.DateTime);
    }

    [Fact]
    public void TruncateToMicroseconds_AlreadyTruncated_ReturnsSameValue()
    {
        var truncated = Whole.TruncateToMicroseconds();

        truncated.Should().Be(Whole);
    }
}
