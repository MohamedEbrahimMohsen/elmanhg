using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Domain.SharedKernel;
using Elmanhg.Domain.Subscriptions;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Subscriptions.Shared;

public sealed class PaymentRefundSettlementTests
{
    private static readonly DateTimeOffset PaidAt = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset RefundedAt = PaidAt.AddDays(3);
    private readonly Guid _studentId = Guid.NewGuid();

    [Fact]
    public void Apply_WithSubscription_MarksRefundedAndRevokesPaidMonths()
    {
        var subscription = Subscription.Start(_studentId, SubscriptionPlan.Base, BillingPeriod.Termly, 4, PaidAt, "txn-1", _studentId);
        var payment = Succeeded(subscription.Id, BillingPeriod.Monthly, 1);
        var adminId = Guid.NewGuid();

        PaymentRefundSettlement.Apply(payment, subscription, "refund-1", RefundedAt, adminId, "Duplicate charge", Guid.NewGuid());

        (payment.Status, payment.RefundTransactionId, payment.RefundedBy).Should().Be((PaymentStatus.Refunded, "refund-1", (Guid?)adminId));
        (subscription.Status, subscription.CurrentPeriodEnd).Should().Be((SubscriptionStatus.Active, PaidAt.AddMonths(3)));
    }

    [Fact]
    public void Apply_WithoutSubscription_MarksRefundedOnly()
    {
        var payment = Succeeded(Guid.NewGuid(), BillingPeriod.Monthly, 1);

        PaymentRefundSettlement.Apply(payment, null, "refund-1", RefundedAt, null, null, null);

        (payment.Status, payment.RefundTransactionId, payment.RefundedAt).Should().Be((PaymentStatus.Refunded, "refund-1", (DateTimeOffset?)RefundedAt));
    }

    private Payment Succeeded(Guid subscriptionId, BillingPeriod period, int months)
    {
        var payment = Payment.Create(_studentId, SubscriptionPlan.Base, period, months, new Money(19900, "EGP"));
        payment.MarkSucceeded(subscriptionId, "txn-1", "{}", PaidAt);
        return payment;
    }
}
