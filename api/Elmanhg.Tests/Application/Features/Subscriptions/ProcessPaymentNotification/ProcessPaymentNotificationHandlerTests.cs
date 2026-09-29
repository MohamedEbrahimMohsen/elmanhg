using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.Payments;
using Elmanhg.Application.Subscriptions.ProcessPaymentNotification;
using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Domain.SharedKernel;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Subscriptions.ProcessPaymentNotification;

public sealed class ProcessPaymentNotificationHandlerTests
{
    private const string Payload = "{\"type\":\"TRANSACTION\"}";
    private const string Signature = "signature";
    private const string OrderId = "217503754";
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly IPaymentRepository _paymentRepository = Substitute.For<IPaymentRepository>();
    private readonly ISubscriptionRepository _subscriptionRepository = Substitute.For<ISubscriptionRepository>();
    private readonly IPaymentNotificationReader _notificationReader = Substitute.For<IPaymentNotificationReader>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly List<Payment> _payments = [];
    private readonly List<Subscription> _subscriptions = [];
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly ProcessPaymentNotificationHandler _handler;

    public ProcessPaymentNotificationHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(Now);
        _paymentRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<Payment, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Payment>, IQueryable<Payment>>?>(), Arg.Any<Func<IQueryable<Payment>, IOrderedQueryable<Payment>>?>(), Arg.Any<bool>())
            .Returns(call => _payments.FirstOrDefault(call.Arg<Expression<Func<Payment, bool>>>().Compile()));
        _subscriptionRepository.FindAsync(Arg.Any<Expression<Func<Subscription, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subscription>, IQueryable<Subscription>>?>(), Arg.Any<Func<IQueryable<Subscription>, IOrderedQueryable<Subscription>>?>(), Arg.Any<bool>())
            .Returns(call => _subscriptions.Where(call.Arg<Expression<Func<Subscription, bool>>>().Compile()).ToList());
        _handler = new ProcessPaymentNotificationHandler(_paymentRepository, _subscriptionRepository, _notificationReader, Microsoft.Extensions.Options.Options.Create(new SubscriptionsOptions()), _timeProvider);
    }

    [Fact]
    public async Task Handle_NotATransaction_ReturnsIgnored()
    {
        _notificationReader.Read(Payload, Signature).Returns((PaymentNotification?)null);

        var result = await Handle();

        result.Should().Be(new PaymentNotificationResult(null, PaymentNotificationOutcome.Ignored));
        await _paymentRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PendingTransaction_ReturnsIgnored()
    {
        var payment = AddPending();
        Notify(payment, "txn-1", succeeded: false, pending: true);

        var result = await Handle();

        (result.Outcome, payment.Status).Should().Be((PaymentNotificationOutcome.Ignored, PaymentStatus.Pending));
        await _paymentRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RefundOrVoid_ReturnsIgnored()
    {
        var payment = AddPending();
        Notify(payment, "txn-1", refundOrVoid: true);

        var result = await Handle();

        (result.Outcome, payment.Status).Should().Be((PaymentNotificationOutcome.Ignored, PaymentStatus.Pending));
        await _paymentRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TransactionAlreadyRecorded_ReturnsDuplicate()
    {
        var payment = AddPending();
        payment.MarkFailed("txn-1", Payload, Now);
        Notify(payment, "txn-1");

        var result = await Handle();

        (result.PaymentId, result.Outcome, payment.Status).Should().Be(((Guid?)payment.Id, PaymentNotificationOutcome.Duplicate, PaymentStatus.Failed));
        await _paymentRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownMerchantOrder_ThrowsPaymentNotFound()
    {
        _notificationReader.Read(Payload, Signature).Returns(new PaymentNotification("txn-1", Guid.NewGuid().ToString(), OrderId, true, false, false, 19900, "EGP"));

        var act = () => Handle();

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.PaymentNotFound);
        await _paymentRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MerchantOrderMissing_MatchesByProviderOrderId()
    {
        var payment = AddPending();
        payment.LinkProviderOrder(OrderId);
        _notificationReader.Read(Payload, Signature).Returns(new PaymentNotification("txn-1", null, OrderId, true, false, false, 19900, "EGP"));

        var result = await Handle();

        (result.PaymentId, result.Outcome, payment.Status).Should().Be(((Guid?)payment.Id, PaymentNotificationOutcome.Succeeded, PaymentStatus.Succeeded));
        await _paymentRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AmountDiffers_ThrowsMismatch()
    {
        var payment = AddPending();
        _notificationReader.Read(Payload, Signature).Returns(new PaymentNotification("txn-1", payment.Id.ToString(), OrderId, true, false, false, 100, "EGP"));

        var act = () => Handle();

        (await act.Should().ThrowAsync<BadRequestCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.PaymentNotificationMismatch);
        payment.Status.Should().Be(PaymentStatus.Pending);
        await _paymentRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ProviderOrderDiffers_ThrowsMismatch()
    {
        var payment = AddPending();
        payment.LinkProviderOrder(OrderId);
        _notificationReader.Read(Payload, Signature).Returns(new PaymentNotification("txn-1", payment.Id.ToString(), "999", true, false, false, 19900, "EGP"));

        var act = () => Handle();

        (await act.Should().ThrowAsync<BadRequestCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.PaymentNotificationMismatch);
        payment.Status.Should().Be(PaymentStatus.Pending);
        await _paymentRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SuccessForPending_StartsSubscriptionAndSaves()
    {
        var payment = AddPending();
        Subscription? added = null;
        _subscriptionRepository.AddAsync(Arg.Do<Subscription>(x => added = x), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        Notify(payment, "txn-1");

        var result = await Handle();

        result.Outcome.Should().Be(PaymentNotificationOutcome.Succeeded);
        (added!.StudentId, added.CurrentPeriodStart, added.CurrentPeriodEnd).Should().Be((_studentId, Now, Now.AddMonths(1)));
        (payment.Status, payment.SubscriptionId, payment.PaymobTransactionId, payment.RawWebhook, payment.CompletedAt).Should().Be((PaymentStatus.Succeeded, (Guid?)added.Id, "txn-1", Payload, (DateTimeOffset?)Now));
        await _paymentRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SuccessWhileBaseHeld_RenewsHeldWithoutAdding()
    {
        var held = new SubscriptionBuilder().ForStudent(_studentId).StartingAt(Now.AddDays(-5)).Build();
        var previousEnd = held.CurrentPeriodEnd;
        _subscriptions.Add(held);
        var payment = AddPending();
        Notify(payment, "txn-1");

        var result = await Handle();

        (result.Outcome, held.CurrentPeriodEnd, payment.SubscriptionId).Should().Be((PaymentNotificationOutcome.Succeeded, previousEnd.AddMonths(1), (Guid?)held.Id));
        await _subscriptionRepository.DidNotReceive().AddAsync(Arg.Any<Subscription>(), Arg.Any<CancellationToken>());
        await _paymentRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SuccessAfterFailedAttempt_MarksSucceeded()
    {
        var payment = AddPending();
        payment.MarkFailed("txn-1", Payload, Now.AddMinutes(-1));
        Notify(payment, "txn-2");

        var result = await Handle();

        (result.Outcome, payment.Status, payment.PaymobTransactionId).Should().Be((PaymentNotificationOutcome.Succeeded, PaymentStatus.Succeeded, "txn-2"));
        await _paymentRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FailureForPending_MarksFailed()
    {
        var payment = AddPending();
        Notify(payment, "txn-1", succeeded: false);

        var result = await Handle();

        (result.Outcome, payment.Status, payment.PaymobTransactionId).Should().Be((PaymentNotificationOutcome.MarkedFailed, PaymentStatus.Failed, "txn-1"));
        await _subscriptionRepository.DidNotReceive().AddAsync(Arg.Any<Subscription>(), Arg.Any<CancellationToken>());
        await _paymentRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FailureAfterSuccess_ReturnsOutOfOrder()
    {
        var payment = AddPending();
        payment.MarkSucceeded(Guid.NewGuid(), "txn-1", Payload, Now);
        Notify(payment, "txn-2", succeeded: false);

        var result = await Handle();

        (result.Outcome, payment.Status, payment.PaymobTransactionId).Should().Be((PaymentNotificationOutcome.OutOfOrder, PaymentStatus.Succeeded, "txn-1"));
        await _paymentRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SecondSuccessForSucceededPayment_ReturnsOutOfOrder()
    {
        var payment = AddPending();
        payment.MarkSucceeded(Guid.NewGuid(), "txn-1", Payload, Now);
        Notify(payment, "txn-2");

        var result = await Handle();

        (result.Outcome, payment.PaymobTransactionId).Should().Be((PaymentNotificationOutcome.OutOfOrder, "txn-1"));
        await _subscriptionRepository.DidNotReceive().AddAsync(Arg.Any<Subscription>(), Arg.Any<CancellationToken>());
        await _paymentRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private Task<PaymentNotificationResult> Handle() => _handler.Handle(new ProcessPaymentNotificationCommand(Payload, Signature), TestContext.Current.CancellationToken);

    private void Notify(Payment payment, string transactionId, bool succeeded = true, bool pending = false, bool refundOrVoid = false)
    {
        _notificationReader.Read(Payload, Signature).Returns(new PaymentNotification(transactionId, payment.Id.ToString(), OrderId, succeeded, pending, refundOrVoid, payment.AmountMinor, payment.Currency));
    }

    private Payment AddPending()
    {
        var payment = Payment.Create(_studentId, SubscriptionPlan.Base, BillingPeriod.Monthly, 1, new Money(19900, "EGP"));
        _payments.Add(payment);
        return payment;
    }
}
