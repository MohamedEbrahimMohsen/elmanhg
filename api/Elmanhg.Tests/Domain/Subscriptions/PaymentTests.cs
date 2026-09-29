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
    private readonly Guid _studentId = Guid.NewGuid();

    [Fact]
    public void Create_ValidAmount_IsPendingWithAmount()
    {
        var payment = Pending();

        (payment.Status, payment.Amount, payment.SubscriptionId).Should().Be((PaymentStatus.Pending, new Money(19900, "EGP"), (Guid?)null));
        (payment.StudentId, payment.Plan, payment.Period).Should().Be((_studentId, SubscriptionPlan.Base, BillingPeriod.Monthly));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_NonPositiveAmount_ThrowsAmountInvalid(long amountMinor)
    {
        var act = () => Payment.Create(_studentId, SubscriptionPlan.Base, BillingPeriod.Monthly, new Money(amountMinor, "EGP"));

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.PaymentAmountInvalid);
    }

    [Theory]
    [InlineData("EG")]
    [InlineData("egp")]
    [InlineData("EGPX")]
    public void Create_InvalidCurrency_ThrowsAmountInvalid(string currency)
    {
        var act = () => Payment.Create(_studentId, SubscriptionPlan.Base, BillingPeriod.Monthly, new Money(19900, currency));

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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MarkSucceeded_NotPending_ThrowsNotPending(bool previouslySucceeded)
    {
        var payment = Completed(previouslySucceeded);

        var act = () => payment.MarkSucceeded(Guid.NewGuid(), "txn-2", Raw, CompletedAt);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.PaymentNotPending);
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

    private Payment Pending() => Payment.Create(_studentId, SubscriptionPlan.Base, BillingPeriod.Monthly, new Money(19900, "EGP"));

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
