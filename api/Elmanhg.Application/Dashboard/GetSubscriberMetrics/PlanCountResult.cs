using Elmanhg.Domain.Subscriptions;

namespace Elmanhg.Application.Dashboard.GetSubscriberMetrics;

public sealed record PlanCountResult(SubscriptionPlan Plan, int Count);
