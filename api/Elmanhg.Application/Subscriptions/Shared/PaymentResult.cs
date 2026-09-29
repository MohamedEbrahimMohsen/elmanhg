using Elmanhg.Domain.SharedKernel;
using Elmanhg.Domain.Subscriptions;

namespace Elmanhg.Application.Subscriptions.Shared;

public sealed record PaymentResult(Guid Id, SubscriptionPlan Plan, BillingPeriod Period, Money Amount, PaymentStatus Status, DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt);
