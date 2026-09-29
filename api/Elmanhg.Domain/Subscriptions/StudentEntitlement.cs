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

    public void EnsureCanPurchase(SubscriptionPlan plan)
    {
        if (plan == SubscriptionPlan.Base && BaseSubscription is not null)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.CheckoutPlanAlreadyActive);
        }

        if (plan == SubscriptionPlan.AskTeacher && BaseSubscription is null)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.CheckoutRequiresBase);
        }

        if (plan == SubscriptionPlan.AskTeacher && AskTeacherSubscription is not null)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.CheckoutPlanAlreadyActive);
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
