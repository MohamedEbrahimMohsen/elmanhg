using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Subscriptions;

namespace Elmanhg.Application.Subscriptions.Shared;

public static class EntitlementResultGenerator
{
    public static EntitlementResult Generate(StudentEntitlement entitlement, SubscriptionsOptions options)
    {
        var subscriptions = new[] { entitlement.BaseSubscription, entitlement.AskTeacherSubscription }
            .OfType<Subscription>()
            .Select(x => Map(x, options.GracePeriod))
            .ToList();
        return entitlement.Tier switch
        {
            PlanTier.Base => new EntitlementResult(PlanTier.Base, entitlement.HasAskTeacher, true, null, options.BaseDailyAvatarMessages, null, entitlement.HasAskTeacher ? options.AskTeacherMonthlyQuestions : 0, subscriptions),
            _ => new EntitlementResult(PlanTier.Free, false, false, options.FreeDailyQuizQuestions, options.FreeDailyAvatarMessages, options.FreeOpenLessonsPerUnit, 0, subscriptions),
        };
    }

    private static SubscriptionResult Map(Subscription subscription, TimeSpan gracePeriod) => new(subscription.Id, subscription.Plan, subscription.Period, subscription.Status, subscription.CurrentPeriodStart, subscription.CurrentPeriodEnd, subscription.EntitledUntil(gracePeriod) ?? subscription.CurrentPeriodEnd, subscription.CancelledAt);
}
