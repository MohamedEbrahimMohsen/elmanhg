using Core.Errors;
using Elmanhg.Domain.SharedKernel;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Domain.Subscriptions;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Subscriptions;

public sealed class PaymentTests
{
    private const string Transaction = "txn-1";
    private const string Raw = "{\"obj\":{\"id\":1}}";
    private static readonly DateTimeOffset CompletedAt = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset RefundedAt = CompletedAt.AddDays(2);
    private readonly Guid _studentId = Guid.NewGuid();

    [Fact]
    public void Create_ValidAmount_IsPendingWithAmount()
    {
        var payment = Pending();

        (payment.Status, payment.Amount, payment.SubscriptionId).Should().Be((PaymentStatus.Pending, new Money(19900, "EGP"), (Guid?)null));
        (payment.StudentId, payment.Plan, payment.Period, payment.PeriodMonths).Should().Be((_studentId, SubscriptionPlan.Base, BillingPeriod.Monthly, 1));
    }

    [Fact]
    public void Create_PeriodMonthsBelowOne_ThrowsPeriodInvalid()
    {
        var act = () => Payment.Create(_studentId, SubscriptionPlan.Base, BillingPeriod.Monthly, 0, new Money(19900, "EGP"));

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.SubscriptionPeriodInvalid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_NonPositiveAmount_ThrowsAmountInvalid(long amountMinor)
    {
        var act = () => Payment.Create(_studentId, SubscriptionPlan.Base, BillingPeriod.Monthly, 1, new Money(amountMinor, "EGP"));

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.PaymentAmountInvalid);
    }

    [Theory]
    [InlineData("EG")]
    [InlineData("egp")]
    [InlineData("EGPX")]
    public void Create_InvalidCurrency_ThrowsAmountInvalid(string currency)
    {
        var act = () => Payment.Create(_studentId, SubscriptionPlan.Base, BillingPeriod.Monthly, 1, new Money(19900, currency));

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.PaymentAmountInvalid);
    }

    [Fact]
    public void MarkSucceeded_Pending_RecordsTransactionAndSubscription()
    {
        var payment = Pending();
        var subscriptionId = Guid.NewGuid();

        payment.MarkSucceeded(subscriptionId, Transaction, Raw, CompletedAt);

        (payment.Status, payment.SubscriptionId, payment.PaymobTransactionId, payment.RawWebhook, payment.CompletedAt).Should().Be((PaymentStatus.Succeeded, (Guid?)subscriptionId, Transaction, Raw, (DateTimeOffset?)CompletedAt));
    }

    [Fact]
    public void MarkFailed_Pending_RecordsTransaction()
    {
        var payment = Pending();

        payment.MarkFailed(Transaction, Raw, CompletedAt);

        (payment.Status, payment.SubscriptionId, payment.PaymobTransactionId, payment.RawWebhook, payment.CompletedAt).Should().Be((PaymentStatus.Failed, (Guid?)null, Transaction, Raw, (DateTimeOffset?)CompletedAt));
    }

    [Fact]
    public void MarkSucceeded_AlreadySucceeded_ThrowsNotPending()
    {
        var payment = Completed(true);

        var act = () => payment.MarkSucceeded(Guid.NewGuid(), "txn-2", Raw, CompletedAt);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.PaymentNotPending);
    }

    [Fact]
    public void MarkSucceeded_Failed_RecordsSuccessAndNewTransaction()
    {
        var payment = Completed(false);
        var subscriptionId = Guid.NewGuid();

        payment.MarkSucceeded(subscriptionId, "txn-2", Raw, CompletedAt.AddMinutes(1));

        (payment.Status, payment.SubscriptionId, payment.PaymobTransactionId, payment.CompletedAt).Should().Be((PaymentStatus.Succeeded, (Guid?)subscriptionId, "txn-2", (DateTimeOffset?)CompletedAt.AddMinutes(1)));
    }

    [Fact]
    public void LinkProviderOrder_Pending_StoresOrderId()
    {
        var payment = Pending();

        payment.LinkProviderOrder("217503754");

        payment.ProviderOrderId.Should().Be("217503754");
    }

    [Fact]
    public void LinkProviderOrder_NotPending_ThrowsNotPending()
    {
        var payment = Completed(false);

        var act = () => payment.LinkProviderOrder("217503754");

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.PaymentNotPending);
        payment.ProviderOrderId.Should().BeNull();
    }

    [Fact]
    public void FlagForReview_SetsReason()
    {
        var payment = Pending();

        payment.FlagForReview(PaymentReviewReason.AskTeacherWithoutBase);

        payment.ReviewReason.Should().Be(PaymentReviewReason.AskTeacherWithoutBase);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MarkFailed_NotPending_ThrowsNotPending(bool previouslySucceeded)
    {
        var payment = Completed(previouslySucceeded);

        var act = () => payment.MarkFailed("txn-2", Raw, CompletedAt);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.PaymentNotPending);
    }

    [Fact]
    public void MarkRefunded_Succeeded_RecordsRefundAndStatus()
    {
        var payment = Completed(true);
        var adminId = Guid.NewGuid();
        var key = Guid.NewGuid();

        payment.MarkRefunded("refund-1", RefundedAt, adminId, "Duplicate charge", key);

        (payment.Status, payment.RefundTransactionId, payment.RefundedAt, payment.RefundedBy).Should().Be((PaymentStatus.Refunded, "refund-1", (DateTimeOffset?)RefundedAt, (Guid?)adminId));
        (payment.RefundReason, payment.RefundIdempotencyKey, payment.IsRefundable).Should().Be(("Duplicate charge", (Guid?)key, false));
    }

    [Fact]
    public void MarkRefunded_FlaggedPayment_ResolvesReview()
    {
        var payment = Completed(true);
        payment.FlagForReview(PaymentReviewReason.AskTeacherWithoutBase);
        var adminId = Guid.NewGuid();

        payment.MarkRefunded("refund-1", RefundedAt, adminId, "Duplicate charge", Guid.NewGuid());

        (payment.NeedsReview, payment.ReviewResolvedAt, payment.ReviewResolvedBy).Should().Be((false, (DateTimeOffset?)RefundedAt, (Guid?)adminId));
    }

    [Fact]
    public void MarkRefunded_AlreadyRefunded_ThrowsAlreadyRefunded()
    {
        var payment = Refunded(Guid.NewGuid());

        var act = () => payment.MarkRefunded("refund-2", RefundedAt, Guid.NewGuid(), "Again", Guid.NewGuid());

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.PaymentAlreadyRefunded);
    }

    [Theory]
    [InlineData(PaymentStatus.Pending)]
    [InlineData(PaymentStatus.Failed)]
    public void MarkRefunded_NotSucceeded_ThrowsNotRefundable(PaymentStatus status)
    {
        var payment = status == PaymentStatus.Pending ? Pending() : Completed(false);

        var act = () => payment.MarkRefunded("refund-1", RefundedAt, Guid.NewGuid(), "Reason", Guid.NewGuid());

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.PaymentNotRefundable);
        payment.Status.Should().Be(status);
    }

    [Fact]
    public void MarkSucceeded_Refunded_ThrowsNotPending()
    {
        var payment = Refunded(Guid.NewGuid());

        var act = () => payment.MarkSucceeded(Guid.NewGuid(), "txn-2", Raw, CompletedAt);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.PaymentNotPending);
    }

    [Fact]
    public void ResolveReview_OpenReview_RecordsResolver()
    {
        var payment = Completed(true);
        payment.FlagForReview(PaymentReviewReason.AskTeacherWithoutBase);
        var adminId = Guid.NewGuid();

        payment.ResolveReview(adminId, RefundedAt);

        (payment.NeedsReview, payment.ReviewResolvedAt, payment.ReviewResolvedBy, payment.Status).Should().Be((false, (DateTimeOffset?)RefundedAt, (Guid?)adminId, PaymentStatus.Succeeded));
    }

    [Fact]
    public void ResolveReview_NotFlagged_ThrowsReviewNotOpen()
    {
        var payment = Completed(true);

        var act = () => payment.ResolveReview(Guid.NewGuid(), RefundedAt);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.PaymentReviewNotOpen);
    }

    [Fact]
    public void ResolveReview_AlreadyResolved_ThrowsReviewNotOpen()
    {
        var payment = Completed(true);
        payment.FlagForReview(PaymentReviewReason.AskTeacherWithoutBase);
        payment.ResolveReview(Guid.NewGuid(), RefundedAt);

        var act = () => payment.ResolveReview(Guid.NewGuid(), RefundedAt.AddMinutes(1));

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.PaymentReviewNotOpen);
    }

    [Fact]
    public void FlagForReview_AfterResolution_ReopensReview()
    {
        var payment = Completed(true);
        payment.FlagForReview(PaymentReviewReason.AskTeacherWithoutBase);
        payment.ResolveReview(Guid.NewGuid(), RefundedAt);

        payment.FlagForReview(PaymentReviewReason.PartialRefundAtProvider);

        (payment.NeedsReview, payment.ReviewReason, payment.ReviewResolvedAt, payment.ReviewResolvedBy).Should().Be((true, (PaymentReviewReason?)PaymentReviewReason.PartialRefundAtProvider, (DateTimeOffset?)null, (Guid?)null));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void IsRefundReplay_ByKey_MatchesOnlySameKeyAfterRefund(bool sameKey, bool expected)
    {
        var key = Guid.NewGuid();
        var payment = Refunded(key);

        var replay = payment.IsRefundReplay(sameKey ? key : Guid.NewGuid());

        replay.Should().Be(expected);
    }

    private Payment Refunded(Guid key)
    {
        var payment = Completed(true);
        payment.MarkRefunded("refund-1", RefundedAt, Guid.NewGuid(), "Duplicate charge", key);
        return payment;
    }

    private Payment Pending() => Payment.Create(_studentId, SubscriptionPlan.Base, BillingPeriod.Monthly, 1, new Money(19900, "EGP"));

    private Payment Completed(bool succeeded)
    {
        var payment = Pending();
        if (succeeded)
        {
            payment.MarkSucceeded(Guid.NewGuid(), Transaction, Raw, CompletedAt);
        }
        else
        {
            payment.MarkFailed(Transaction, Raw, CompletedAt);
        }

        return payment;
    }
}
