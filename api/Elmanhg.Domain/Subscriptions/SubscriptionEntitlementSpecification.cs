using System.Linq.Expressions;

namespace Elmanhg.Domain.Subscriptions;

// PRD §11.2: the single definition of a subscription that grants access; derived from status and dates, never stored.
public static class SubscriptionEntitlementSpecification
{
    public static Expression<Func<Subscription, bool>> EntitledFor(Guid studentId, DateTimeOffset now, TimeSpan gracePeriod)
    {
        var lapsedBefore = now - gracePeriod;
        return x => x.StudentId == studentId
            && (((x.Status == SubscriptionStatus.Active || x.Status == SubscriptionStatus.PastDue) && x.CurrentPeriodEnd > lapsedBefore)
                || (x.Status == SubscriptionStatus.Cancelled && x.CurrentPeriodEnd > now));
    }
}
