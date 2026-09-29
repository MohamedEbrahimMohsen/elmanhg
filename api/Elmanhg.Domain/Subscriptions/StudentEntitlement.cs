using Core.Errors;
using Elmanhg.Domain.SharedKernel.Exceptions;

namespace Elmanhg.Domain.Subscriptions;

public sealed record StudentEntitlement(Subscription? BaseSubscription, Subscription? AskTeacherSubscription)
{
    public static StudentEntitlement Free { get; } = new(null, null);

    public PlanTier Tier => BaseSubscription is null ? PlanTier.Free : PlanTier.Base;

    public bool HasAskTeacher => BaseSubscription is not null && AskTeacherSubscription is not null;

    public static StudentEntitlement Resolve(IEnumerable<Subscription> subscriptions, DateTimeOffset now, TimeSpan gracePeriod)
    {
        var entitled = subscriptions
            .Where(x => x.IsEntitledAt(now, gracePeriod))
            .ToList();
        return new StudentEntitlement(Latest(entitled, SubscriptionPlan.Base, gracePeriod), Latest(entitled, SubscriptionPlan.AskTeacher, gracePeriod));
    }

    public string? PurchaseConflict(SubscriptionPlan plan) => plan switch
    {
        SubscriptionPlan.Base when BaseSubscription is not null => ErrorCodes.CheckoutPlanAlreadyActive,
        SubscriptionPlan.AskTeacher when BaseSubscription is null => ErrorCodes.CheckoutRequiresBase,
        SubscriptionPlan.AskTeacher when AskTeacherSubscription is not null => ErrorCodes.CheckoutPlanAlreadyActive,
        _ => null,
    };

    public void EnsureCanPurchase(SubscriptionPlan plan)
    {
        var conflict = PurchaseConflict(plan);
        if (conflict is not null)
        {
            throw new BusinessRuleViolationCoreException(conflict);
        }
    }

    private static Subscription? Latest(List<Subscription> entitled, SubscriptionPlan plan, TimeSpan gracePeriod)
    {
        return entitled
            .Where(x => x.Plan == plan)
            .OrderByDescending(x => x.EntitledUntil(gracePeriod))
            .FirstOrDefault();
    }
}
