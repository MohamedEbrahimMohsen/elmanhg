using Elmanhg.Domain.Subscriptions;

namespace Elmanhg.Application.Subscriptions.Shared;

public static class AdminSubscriptionResultGenerator
{
    public static AdminSubscriptionResult Generate(Subscription subscription, TimeSpan gracePeriod) => new(subscription.Id, subscription.Plan, subscription.Period, subscription.Status, subscription.CurrentPeriodStart, subscription.CurrentPeriodEnd, subscription.EntitledUntil(gracePeriod), subscription.PaymobReference is null);
}
