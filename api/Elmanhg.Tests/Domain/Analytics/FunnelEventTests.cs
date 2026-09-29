using Elmanhg.Domain.Analytics;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Analytics;

public sealed class FunnelEventTests
{
    [Fact]
    public void Record_AnyInput_SetsEveryField()
    {
        var anonymousId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var occurredAt = new DateTimeOffset(2026, 9, 29, 10, 0, 0, TimeSpan.Zero);

        var funnelEvent = FunnelEvent.Record(anonymousId, userId, FunnelEventType.SignUpCompleted, occurredAt);

        funnelEvent.AnonymousId.Should().Be(anonymousId);
        funnelEvent.UserId.Should().Be(userId);
        funnelEvent.Type.Should().Be(FunnelEventType.SignUpCompleted);
        funnelEvent.OccurredAt.Should().Be(occurredAt);
        funnelEvent.Id.Should().NotBeEmpty();
    }
}
