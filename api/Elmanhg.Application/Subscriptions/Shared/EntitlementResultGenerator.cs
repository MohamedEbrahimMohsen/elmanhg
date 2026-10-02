using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Subscriptions;

namespace Elmanhg.Application.Subscriptions.Shared;

public static class EntitlementResultGenerator
{
    public static EntitlementResult Generate(StudentEntitlement entitlement, SubscriptionsOptions options, PlanLimits limits, DateTimeOffset now)
    {
        var subscriptions = new[] { entitlement.BaseSubscription, entitlement.AskTeacherSubscription }
            .OfType<Subscription>()
            .Select(x => Map(x, entitlement, options, now))
            .ToList();
        return entitlement.Tier switch
        {
            PlanTier.Base => new EntitlementResult(PlanTier.Base, entitlement.HasAskTeacher, true, null, limits.BaseDailyAvatarMessages, null, entitlement.HasAskTeacher ? limits.AskTeacherMonthlyQuestions : 0, subscriptions),
            _ => new EntitlementResult(PlanTier.Free, false, false, limits.FreeDailyQuizQuestions, limits.FreeDailyAvatarMessages, limits.FreeOpenLessonsPerUnit, 0, subscriptions),
        };
    }

    private static SubscriptionResult Map(Subscription subscription, StudentEntitlement entitlement, SubscriptionsOptions options, DateTimeOffset now) => new(subscription.Id, subscription.Plan, subscription.Period, subscription.Status, subscription.CurrentPeriodStart, subscription.CurrentPeriodEnd, subscription.EntitledUntil(options.GracePeriod) ?? subscription.CurrentPeriodEnd, subscription.CancelledAt, subscription.Status is SubscriptionStatus.Active or SubscriptionStatus.PastDue && subscription.CurrentPeriodEnd <= now, entitlement.PurchaseConflict(subscription.Plan, now, options.RenewalWindow) is null);
}
