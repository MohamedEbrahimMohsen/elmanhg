using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.Payments;
using Elmanhg.Application.Subscriptions.ProcessPaymentNotification;
using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Domain.SharedKernel;
using Elmanhg.Domain.Subscriptions;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Subscriptions.ProcessPaymentNotification;

public sealed class ProcessPaymentNotificationReversalTests
{
    private const string Payload = "{\"type\":\"TRANSACTION\"}";
    private const string Signature = "signature";
    private const string OrderId = "217503754";
    private const string RefundId = "refund-txn-1";
    private static readonly DateTimeOffset PaidAt = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = PaidAt.AddDays(5);
    private readonly IPaymentRepository _paymentRepository = Substitute.For<IPaymentRepository>();
    private readonly ISubscriptionRepository _subscriptionRepository = Substitute.For<ISubscriptionRepository>();
    private readonly IPaymentNotificationReader _notificationReader = Substitute.For<IPaymentNotificationReader>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly List<Payment> _payments = [];
    private readonly List<Subscription> _subscriptions = [];
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly ProcessPaymentNotificationHandler _handler;

    public ProcessPaymentNotificationReversalTests()
    {
        _timeProvider.GetUtcNow().Returns(Now);
        _paymentRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<Payment, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Payment>, IQueryable<Payment>>?>(), Arg.Any<Func<IQueryable<Payment>, IOrderedQueryable<Payment>>?>(), Arg.Any<bool>())
            .Returns(call => _payments.FirstOrDefault(call.Arg<Expression<Func<Payment, bool>>>().Compile()));
        _subscriptionRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<Subscription, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subscription>, IQueryable<Subscription>>?>(), Arg.Any<Func<IQueryable<Subscription>, IOrderedQueryable<Subscription>>?>(), Arg.Any<bool>())
            .Returns(call => _subscriptions.FirstOrDefault(call.Arg<Expression<Func<Subscription, bool>>>().Compile()));
        _handler = new ProcessPaymentNotificationHandler(_paymentRepository, _subscriptionRepository, _notificationReader, Microsoft.Extensions.Options.Options.Create(new SubscriptionsOptions()), _timeProvider);
    }

    [Fact]
    public async Task Handle_FullReversalForSucceeded_MarksRefundedAndRevokesSubscription()
    {
        var (payment, subscription) = AddSucceeded();
        Reverse(payment, 19900);

        var result = await Handle();

        (result.PaymentId, result.Outcome).Should().Be(((Guid?)payment.Id, PaymentNotificationOutcome.Refunded));
        (payment.Status, payment.RefundTransactionId, payment.RefundedAt, payment.RefundedBy).Should().Be((PaymentStatus.Refunded, RefundId, (DateTimeOffset?)Now, (Guid?)null));
        (subscription.Status, subscription.ExpiredAt).Should().Be((SubscriptionStatus.Expired, (DateTimeOffset?)Now));
        await _paymentRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReversalAlreadyRecorded_ReturnsDuplicate()
    {
        var (payment, _) = AddSucceeded();
        payment.MarkRefunded(RefundId, Now, Guid.NewGuid(), "Duplicate charge", Guid.NewGuid());
        Reverse(payment, 19900);

        var result = await Handle();

        (result.PaymentId, result.Outcome).Should().Be(((Guid?)payment.Id, PaymentNotificationOutcome.Duplicate));
        await _paymentRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReversalForRefundedPayment_ReturnsDuplicate()
    {
        var (payment, _) = AddSucceeded();
        payment.MarkRefunded("fake-refund-1", Now, Guid.NewGuid(), "Duplicate charge", Guid.NewGuid());
        Reverse(payment, 19900);

        var result = await Handle();

        (result.Outcome, payment.RefundTransactionId).Should().Be((PaymentNotificationOutcome.Duplicate, "fake-refund-1"));
        await _paymentRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReversalForPendingPayment_ThrowsNotSettled()
    {
        var payment = AddPending();
        Reverse(payment, 19900);

        var act = () => Handle();

        (await act.Should().ThrowAsync<ConflictCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.PaymentNotSettled);
        payment.Status.Should().Be(PaymentStatus.Pending);
        await _paymentRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReversalForFailedPayment_ReturnsOutOfOrder()
    {
        var payment = AddPending();
        payment.MarkFailed("txn-1", Payload, PaidAt);
        Reverse(payment, 19900);

        var result = await Handle();

        (result.Outcome, payment.Status).Should().Be((PaymentNotificationOutcome.OutOfOrder, PaymentStatus.Failed));
        await _paymentRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PartialReversal_FlagsForReviewWithoutRevoking()
    {
        var (payment, subscription) = AddSucceeded();
        var end = subscription.CurrentPeriodEnd;
        Reverse(payment, 5000);

        var result = await Handle();

        (result.Outcome, payment.ReviewReason, payment.Status).Should().Be((PaymentNotificationOutcome.FlaggedForReview, (PaymentReviewReason?)PaymentReviewReason.PartialRefundAtProvider, PaymentStatus.Succeeded));
        (subscription.Status, subscription.CurrentPeriodEnd).Should().Be((SubscriptionStatus.Active, end));
        await _paymentRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReversalAboveAmount_ThrowsMismatch()
    {
        var (payment, _) = AddSucceeded();
        Reverse(payment, 19901);

        var act = () => Handle();

        (await act.Should().ThrowAsync<BadRequestCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.PaymentNotificationMismatch);
        payment.Status.Should().Be(PaymentStatus.Succeeded);
        await _paymentRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReversalCurrencyDiffers_ThrowsMismatch()
    {
        var (payment, _) = AddSucceeded();
        Reverse(payment, 19900, currency: "USD");

        var act = () => Handle();

        (await act.Should().ThrowAsync<BadRequestCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.PaymentNotificationMismatch);
        await _paymentRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReversalForUnknownPayment_ThrowsPaymentNotFound()
    {
        _notificationReader.Read(Payload, Signature).Returns(new PaymentNotification(RefundId, Guid.NewGuid().ToString(), OrderId, true, false, PaymentNotificationKind.Reversal, 19900, "EGP"));

        var act = () => Handle();

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.PaymentNotFound);
        await _paymentRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReversalAmountNotPositive_ThrowsMismatch()
    {
        var (payment, subscription) = AddSucceeded();
        Reverse(payment, 0);

        var act = () => Handle();

        (await act.Should().ThrowAsync<BadRequestCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.PaymentNotificationMismatch);
        (payment.Status, payment.ReviewReason, subscription.Status).Should().Be((PaymentStatus.Succeeded, (PaymentReviewReason?)null, SubscriptionStatus.Active));
        await _paymentRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ReversalProviderOrderDiffers_ThrowsMismatch()
    {
        var (payment, subscription) = AddSucceeded(providerOrderId: "999999999");
        Reverse(payment, 19900);

        var act = () => Handle();

        (await act.Should().ThrowAsync<BadRequestCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.PaymentNotificationMismatch);
        (payment.Status, subscription.Status).Should().Be((PaymentStatus.Succeeded, SubscriptionStatus.Active));
        await _paymentRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FailedReversal_ReturnsIgnored()
    {
        var (payment, subscription) = AddSucceeded();
        Reverse(payment, 19900, succeeded: false);

        var result = await Handle();

        (result.Outcome, payment.Status, subscription.Status).Should().Be((PaymentNotificationOutcome.Ignored, PaymentStatus.Succeeded, SubscriptionStatus.Active));
        await _paymentRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private Task<PaymentNotificationResult> Handle() => _handler.Handle(new ProcessPaymentNotificationCommand(Payload, Signature), TestContext.Current.CancellationToken);

    private void Reverse(Payment payment, long amountMinor, bool succeeded = true, string currency = "EGP")
    {
        _notificationReader.Read(Payload, Signature).Returns(new PaymentNotification(RefundId, payment.Id.ToString(), OrderId, succeeded, false, PaymentNotificationKind.Reversal, amountMinor, currency));
    }

    private Payment AddPending()
    {
        var payment = Payment.Create(_studentId, SubscriptionPlan.Base, BillingPeriod.Monthly, 1, new Money(19900, "EGP"));
        _payments.Add(payment);
        return payment;
    }

    private (Payment Payment, Subscription Subscription) AddSucceeded(string? providerOrderId = null)
    {
        var payment = AddPending();
        if (providerOrderId is not null)
        {
            payment.LinkProviderOrder(providerOrderId);
        }

        var subscription = Subscription.Start(_studentId, SubscriptionPlan.Base, BillingPeriod.Monthly, 1, PaidAt, "txn-1", _studentId);
        payment.MarkSucceeded(subscription.Id, "txn-1", Payload, PaidAt);
        _subscriptions.Add(subscription);
        return (payment, subscription);
    }
}
