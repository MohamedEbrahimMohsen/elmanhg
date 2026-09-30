using Core.Auditing;
using Elmanhg.Domain.Subscriptions;

namespace Elmanhg.Application.Subscriptions.Shared;

public sealed record AdminSubscriptionResult(Guid Id, SubscriptionPlan Plan, BillingPeriod Period, SubscriptionStatus Status, DateTimeOffset CurrentPeriodStart, DateTimeOffset CurrentPeriodEnd, DateTimeOffset? EntitledUntil, bool IsComplimentary) : IAuditableResult
{
    Guid? IAuditableResult.AuditResourceId => Id;
}
