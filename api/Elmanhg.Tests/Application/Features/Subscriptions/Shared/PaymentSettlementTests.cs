using Core.Errors;
using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Domain.SharedKernel;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Subscriptions.Shared;

public sealed class PaymentSettlementTests
{
    private const string Transaction = "txn-42";
    private const string Raw = "{\"success\":true}";
    private static readonly DateTimeOffset CompletedAt = SubscriptionBuilder.DefaultStart.AddDays(10);
    private static readonly TimeSpan Grace = TimeSpan.FromDays(3);
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Payment _payment;

    public PaymentSettlementTests()
    {
        _payment = Payment.Create(_studentId, SubscriptionPlan.Base, BillingPeriod.Termly, 4, new Money(69900, "EGP"));
    }

    [Fact]
    public void Succeed_NoHeldPlan_StartsSubscriptionForSnapshotMonths()
    {
        var subscription = PaymentSettlement.Succeed(_payment, StudentEntitlement.Free, Transaction, Raw, Grace, CompletedAt);

        (subscription!.StudentId, subscription.Plan, subscription.Period, subscription.Status).Should().Be((_studentId, SubscriptionPlan.Base, BillingPeriod.Termly, SubscriptionStatus.Active));
        (subscription.CurrentPeriodStart, subscription.CurrentPeriodEnd, subscription.PaymobReference).Should().Be((CompletedAt, CompletedAt.AddMonths(4), Transaction));
        (_payment.Status, _payment.SubscriptionId, _payment.PaymobTransactionId, _payment.RawWebhook, _payment.CompletedAt).Should().Be((PaymentStatus.Succeeded, (Guid?)subscription.Id, Transaction, Raw, (DateTimeOffset?)CompletedAt));
    }

    [Fact]
    public void Fail_Pending_MarksFailed()
    {
        PaymentSettlement.Fail(_payment, Transaction, Raw, CompletedAt);

        (_payment.Status, _payment.PaymobTransactionId, _payment.SubscriptionId).Should().Be((PaymentStatus.Failed, Transaction, (Guid?)null));
    }

    [Fact]
    public void Succeed_PaymentAlreadySucceeded_ThrowsPaymentNotPending()
    {
        var held = HeldBase();
        PaymentSettlement.Succeed(_payment, Entitlement(held), Transaction, Raw, Grace, CompletedAt);
        var endAfterFirst = held.CurrentPeriodEnd;

        var act = () => PaymentSettlement.Succeed(_payment, Entitlement(held), "txn-43", Raw, Grace, CompletedAt);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.PaymentNotPending);
        held.CurrentPeriodEnd.Should().Be(endAfterFirst);
    }

    [Fact]
    public void Succeed_BaseHeld_RenewsHeldAndReturnsNull()
    {
        var held = HeldBase();
        var previousEnd = held.CurrentPeriodEnd;

        var subscription = PaymentSettlement.Succeed(_payment, Entitlement(held), Transaction, Raw, Grace, CompletedAt);

        subscription.Should().BeNull();
        (held.CurrentPeriodStart, held.CurrentPeriodEnd, held.Period).Should().Be((previousEnd, previousEnd.AddMonths(4), BillingPeriod.Termly));
        (_payment.Status, _payment.SubscriptionId).Should().Be((PaymentStatus.Succeeded, (Guid?)held.Id));
    }

    [Fact]
    public void Succeed_CancelledBaseHeld_ResumesHeld()
    {
        var held = new SubscriptionBuilder().ForStudent(_studentId).InStatus(SubscriptionStatus.Cancelled).Build();

        PaymentSettlement.Succeed(_payment, Entitlement(held), Transaction, Raw, Grace, CompletedAt);

        (held.Status, held.CancelledAt).Should().Be((SubscriptionStatus.Active, (DateTimeOffset?)null));
    }

    [Fact]
    public void Succeed_AskTeacherWithoutBase_StartsAndFlagsForReview()
    {
        var payment = Payment.Create(_studentId, SubscriptionPlan.AskTeacher, BillingPeriod.Monthly, 1, new Money(9900, "EGP"));

        var subscription = PaymentSettlement.Succeed(payment, StudentEntitlement.Free, Transaction, Raw, Grace, CompletedAt);

        (subscription!.Plan, payment.Status, payment.ReviewReason).Should().Be((SubscriptionPlan.AskTeacher, PaymentStatus.Succeeded, (PaymentReviewReason?)PaymentReviewReason.AskTeacherWithoutBase));
    }

    [Fact]
    public void Succeed_AskTeacherWithBase_DoesNotFlag()
    {
        var payment = Payment.Create(_studentId, SubscriptionPlan.AskTeacher, BillingPeriod.Monthly, 1, new Money(9900, "EGP"));

        var subscription = PaymentSettlement.Succeed(payment, Entitlement(HeldBase()), Transaction, Raw, Grace, CompletedAt);

        (subscription!.Plan, payment.ReviewReason).Should().Be((SubscriptionPlan.AskTeacher, (PaymentReviewReason?)null));
    }

    private Subscription HeldBase() => new SubscriptionBuilder().ForStudent(_studentId).Build();

    private static StudentEntitlement Entitlement(Subscription held) => StudentEntitlement.Resolve([held], CompletedAt, Grace);
}
