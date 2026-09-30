using System.Linq.Expressions;

namespace Elmanhg.Domain.Subscriptions;

public static class SubscriptionChurnSpecification
{
    public static Expression<Func<Subscription, bool>> ChurnedBetween(DateTimeOffset start, DateTimeOffset end) => x => (x.Status == SubscriptionStatus.Cancelled || x.Status == SubscriptionStatus.Expired) && (x.CancelledAt ?? x.ExpiredAt) >= start && (x.CancelledAt ?? x.ExpiredAt) < end;
}
