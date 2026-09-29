using Core.Errors;
using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Domain.SharedKernel;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Domain.Subscriptions;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Subscriptions.Shared;

public sealed class PaymentSettlementTests
{
    private const string Transaction = "txn-42";
    private const string Raw = "{\"success\":true}";
    private static readonly DateTimeOffset CompletedAt = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly Payment _payment = Payment.Create(Guid.NewGuid(), SubscriptionPlan.Base, BillingPeriod.Termly, new Money(69900, "EGP"));

    [Fact]
    public void Settle_Succeeded_StartsSubscriptionForPaymentPlan()
    {
        var subscription = PaymentSettlement.Settle(_payment, true, Transaction, Raw, 4, CompletedAt);

        (subscription!.StudentId, subscription.Plan, subscription.Period, subscription.Status).Should().Be((_payment.StudentId, SubscriptionPlan.Base, BillingPeriod.Termly, SubscriptionStatus.Active));
        (subscription.CurrentPeriodStart, subscription.CurrentPeriodEnd, subscription.PaymobReference).Should().Be((CompletedAt, CompletedAt.AddMonths(4), Transaction));
        (_payment.Status, _payment.SubscriptionId, _payment.PaymobTransactionId, _payment.RawWebhook, _payment.CompletedAt).Should().Be((PaymentStatus.Succeeded, (Guid?)subscription.Id, Transaction, Raw, (DateTimeOffset?)CompletedAt));
    }

    [Fact]
    public void Settle_Failed_MarksFailedAndReturnsNull()
    {
        var subscription = PaymentSettlement.Settle(_payment, false, Transaction, Raw, 4, CompletedAt);

        subscription.Should().BeNull();
        (_payment.Status, _payment.PaymobTransactionId, _payment.SubscriptionId).Should().Be((PaymentStatus.Failed, Transaction, (Guid?)null));
    }

    [Fact]
    public void Settle_PaymentNotPending_ThrowsPaymentNotPending()
    {
        PaymentSettlement.Settle(_payment, false, Transaction, Raw, 4, CompletedAt);

        var act = () => PaymentSettlement.Settle(_payment, true, Transaction, Raw, 4, CompletedAt);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.PaymentNotPending);
    }
}
