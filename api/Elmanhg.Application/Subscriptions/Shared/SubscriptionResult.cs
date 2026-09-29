using Elmanhg.Domain.Subscriptions;

namespace Elmanhg.Application.Subscriptions.Shared;

public sealed record SubscriptionResult(Guid Id, SubscriptionPlan Plan, BillingPeriod Period, SubscriptionStatus Status, DateTimeOffset CurrentPeriodStart, DateTimeOffset CurrentPeriodEnd, DateTimeOffset EntitledUntil, DateTimeOffset? CancelledAt, bool InGracePeriod, bool CanRenew);
