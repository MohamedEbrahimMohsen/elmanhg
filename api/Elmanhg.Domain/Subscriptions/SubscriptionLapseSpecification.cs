using System.Linq.Expressions;

namespace Elmanhg.Domain.Subscriptions;

public static class SubscriptionLapseSpecification
{
    public static Expression<Func<Subscription, bool>> DueAt(DateTimeOffset now, TimeSpan gracePeriod, IReadOnlyCollection<Guid> excludedIds)
    {
        var lapsedBefore = now - gracePeriod;
        return x => !excludedIds.Contains(x.Id)
            && ((x.Status == SubscriptionStatus.Active && x.CurrentPeriodEnd <= now)
                || (x.Status == SubscriptionStatus.PastDue && x.CurrentPeriodEnd <= lapsedBefore)
                || (x.Status == SubscriptionStatus.Cancelled && x.CurrentPeriodEnd <= now));
    }
}
