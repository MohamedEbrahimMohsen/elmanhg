using Elmanhg.Infrastructure.Messaging;
using FluentAssertions;

namespace Elmanhg.Tests.Infrastructure.Messaging;

public sealed class ReminderDeadlineFormatterTests
{
    [Fact]
    public void Format_UtcInstant_ConvertsToConfiguredZone()
    {
        var formatted = ReminderDeadlineFormatter.Format(new DateTimeOffset(2026, 12, 1, 10, 0, 0, TimeSpan.Zero), "Africa/Cairo");

        formatted.Should().Be("2026-12-01 12:00");
    }
}
