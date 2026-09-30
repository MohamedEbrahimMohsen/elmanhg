using Elmanhg.Domain.Analytics;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Analytics;

public sealed class UserActivityDayTests
{
    [Fact]
    public void Record_SetsUserDayAndFirstSeenAt()
    {
        var userId = Guid.NewGuid();
        var day = new DateOnly(2026, 1, 15);
        var seenAt = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

        var activity = UserActivityDay.Record(userId, day, seenAt);

        activity.Id.Should().NotBeEmpty();
        activity.UserId.Should().Be(userId);
        activity.Day.Should().Be(day);
        activity.FirstSeenAt.Should().Be(seenAt);
    }
}
