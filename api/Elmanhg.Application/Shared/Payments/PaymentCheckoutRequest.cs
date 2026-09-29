using Elmanhg.Domain.SharedKernel;
using Elmanhg.Domain.Subscriptions;

namespace Elmanhg.Application.Shared.Payments;

public sealed record PaymentCheckoutRequest(Guid PaymentId, Money Amount, SubscriptionPlan Plan, BillingPeriod Period, PaymentCustomer Customer);
