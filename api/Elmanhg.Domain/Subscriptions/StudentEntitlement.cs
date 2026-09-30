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

    public Subscription? Held(SubscriptionPlan plan) => plan == SubscriptionPlan.Base ? BaseSubscription : AskTeacherSubscription;

    public string? PurchaseConflict(SubscriptionPlan plan, DateTimeOffset now, TimeSpan renewalWindow) => plan switch
    {
        SubscriptionPlan.AskTeacher when BaseSubscription is null => ErrorCodes.CheckoutRequiresBase,
        _ when Held(plan) is { } held && !held.IsRenewableAt(now, renewalWindow) => ErrorCodes.CheckoutPlanAlreadyActive,
        _ => null,
    };

    public void EnsureCanPurchase(SubscriptionPlan plan, DateTimeOffset now, TimeSpan renewalWindow)
    {
        var conflict = PurchaseConflict(plan, now, renewalWindow);
        if (conflict is not null)
        {
            throw new BusinessRuleViolationCoreException(conflict);
        }
    }

    public void EnsureCanGrant(SubscriptionPlan plan)
    {
        if (plan == SubscriptionPlan.AskTeacher && BaseSubscription is null)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.ComplimentaryRequiresBase);
        }

        if (Held(plan) is not null)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.ComplimentaryPlanAlreadyActive);
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
