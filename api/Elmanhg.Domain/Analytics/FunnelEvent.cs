using Core.DDD.Entities;

namespace Elmanhg.Domain.Analytics;

public class FunnelEvent : Entity
{
    public Guid AnonymousId { get; private set; }
    public Guid? UserId { get; private set; }
    public FunnelEventType Type { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    private FunnelEvent(Guid id) : base(id) { }

    public static FunnelEvent Record(Guid anonymousId, Guid? userId, FunnelEventType type, DateTimeOffset occurredAt)
    {
        return new FunnelEvent(Guid.NewGuid())
        {
            AnonymousId = anonymousId,
            UserId = userId,
            Type = type,
            OccurredAt = occurredAt,
        };
    }
}
