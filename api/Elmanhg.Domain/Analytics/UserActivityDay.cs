using Core.DDD.Entities;

namespace Elmanhg.Domain.Analytics;

public class UserActivityDay : Entity
{
    public Guid UserId { get; private set; }
    public DateOnly Day { get; private set; }
    public DateTimeOffset FirstSeenAt { get; private set; }

    private UserActivityDay(Guid id) : base(id) { }

    public static UserActivityDay Record(Guid userId, DateOnly day, DateTimeOffset seenAt)
    {
        return new UserActivityDay(Guid.NewGuid())
        {
            UserId = userId,
            Day = day,
            FirstSeenAt = seenAt,
        };
    }
}
