using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Payments.RefundPayment;
using Elmanhg.Application.Shared.Payments;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.SharedKernel;
using Elmanhg.Domain.Subscriptions;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Linq.Expressions;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Application.Features.Payments.RefundPayment;

public sealed class RefundPaymentHandlerTests
{
    private const string GatewayRefundId = "refund-777";
    private static readonly DateTimeOffset PaidAt = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = PaidAt.AddDays(5);
    private readonly IPaymentRepository _paymentRepository = Substitute.For<IPaymentRepository>();
    private readonly ISubscriptionRepository _subscriptionRepository = Substitute.For<ISubscriptionRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IPaymentGateway _paymentGateway = Substitute.For<IPaymentGateway>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly List<Payment> _payments = [];
    private readonly List<Subscription> _subscriptions = [];
    private readonly User _student = User.CreateStudentWithEmail("Mona Ali", "mona@example.com");
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly RefundPaymentHandler _handler;

    public RefundPaymentHandlerTests()
    {
        _currentUserService.UserId.Returns(_adminId);
        _timeProvider.GetUtcNow().Returns(Now);
        _paymentRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<Payment, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Payment>, IQueryable<Payment>>?>(), Arg.Any<Func<IQueryable<Payment>, IOrderedQueryable<Payment>>?>(), Arg.Any<bool>())
            .Returns(call => _payments.FirstOrDefault(call.Arg<Expression<Func<Payment, bool>>>().Compile()));
        _subscriptionRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<Subscription, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subscription>, IQueryable<Subscription>>?>(), Arg.Any<Func<IQueryable<Subscription>, IOrderedQueryable<Subscription>>?>(), Arg.Any<bool>())
            .Returns(call => _subscriptions.FirstOrDefault(call.Arg<Expression<Func<Subscription, bool>>>().Compile()));
        _userRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<User>, IQueryable<User>>?>(), Arg.Any<Func<IQueryable<User>, IOrderedQueryable<User>>?>(), Arg.Any<bool>())
            .Returns(call => new[] { _student }.FirstOrDefault(call.Arg<Expression<Func<User, bool>>>().Compile()));
        _paymentGateway.RefundAsync(Arg.Any<PaymentRefundRequest>(), Arg.Any<CancellationToken>()).Returns(new PaymentRefund(GatewayRefundId));
        _handler = new RefundPaymentHandler(_paymentRepository, _subscriptionRepository, _userRepository, _paymentGateway, _currentUserService, _timeProvider);
    }

    [Fact]
    public async Task Handle_SucceededPayment_RefundsThroughGatewayAndRevokesSubscription()
    {
        var (payment, subscription) = AddSucceeded();
        var key = Guid.NewGuid();

        var result = await Handle(payment.Id, "  Duplicate charge  ", key);

        await _paymentGateway.Received(1).RefundAsync(new PaymentRefundRequest(payment.Id, "txn-1", new Money(19900, "EGP")), Arg.Any<CancellationToken>());
        (payment.Status, payment.RefundTransactionId, payment.RefundedBy, payment.RefundReason, payment.RefundIdempotencyKey).Should().Be((PaymentStatus.Refunded, GatewayRefundId, (Guid?)_adminId, "Duplicate charge", (Guid?)key));
        (subscription.Status, subscription.CurrentPeriodEnd).Should().Be((SubscriptionStatus.Expired, Now));
        (result.Status, result.CanRefund, result.StudentName, result.RefundTransactionId).Should().Be((PaymentStatus.Refunded, false, "Mona Ali", GatewayRefundId));
        await _paymentRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FlaggedPayment_ResolvesReviewWithRefund()
    {
        var (payment, _) = AddSucceeded();
        payment.FlagForReview(PaymentReviewReason.AskTeacherWithoutBase);

        var result = await Handle(payment.Id, "Paid without Base", Guid.NewGuid());

        (result.NeedsReview, result.ReviewResolvedAt).Should().Be((false, (DateTimeOffset?)Now));
        await _paymentRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SameIdempotencyKeyAfterRefund_ReplaysWithoutGatewayCall()
    {
        var (payment, _) = AddSucceeded();
        var key = Guid.NewGuid();
        payment.MarkRefunded("refund-1", Now, _adminId, "Duplicate charge", key);

        var result = await Handle(payment.Id, "Duplicate charge", key);

        (result.Status, result.RefundTransactionId).Should().Be((PaymentStatus.Refunded, "refund-1"));
        await _paymentGateway.DidNotReceive().RefundAsync(Arg.Any<PaymentRefundRequest>(), Arg.Any<CancellationToken>());
        await _paymentRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AlreadyRefundedWithOtherKey_ThrowsAlreadyRefunded()
    {
        var (payment, _) = AddSucceeded();
        payment.MarkRefunded("refund-1", Now, _adminId, "Duplicate charge", Guid.NewGuid());

        var act = () => Handle(payment.Id, "Again", Guid.NewGuid());

        (await act.Should().ThrowAsync<BusinessRuleViolationCoreException>()).Which.ErrorCode.Should().Be(DomainErrorCodes.PaymentAlreadyRefunded);
        await _paymentGateway.DidNotReceive().RefundAsync(Arg.Any<PaymentRefundRequest>(), Arg.Any<CancellationToken>());
        await _paymentRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FailedPayment_ThrowsNotRefundable()
    {
        var payment = Payment.Create(_student.Id, SubscriptionPlan.Base, BillingPeriod.Monthly, 1, new Money(19900, "EGP"));
        payment.MarkFailed("txn-1", "{}", PaidAt);
        _payments.Add(payment);

        var act = () => Handle(payment.Id, "Reason", Guid.NewGuid());

        (await act.Should().ThrowAsync<BusinessRuleViolationCoreException>()).Which.ErrorCode.Should().Be(DomainErrorCodes.PaymentNotRefundable);
        await _paymentGateway.DidNotReceive().RefundAsync(Arg.Any<PaymentRefundRequest>(), Arg.Any<CancellationToken>());
        await _paymentRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownPayment_ThrowsPaymentNotFound()
    {
        var act = () => Handle(Guid.NewGuid(), "Reason", Guid.NewGuid());

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.PaymentNotFound);
        await _paymentRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_GatewayDeclines_PropagatesAndSavesNothing()
    {
        var (payment, _) = AddSucceeded();
        _paymentGateway.RefundAsync(Arg.Any<PaymentRefundRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(new BusinessRuleViolationCoreException(ErrorCodes.PaymentRefundDeclined));

        var act = () => Handle(payment.Id, "Reason", Guid.NewGuid());

        (await act.Should().ThrowAsync<BusinessRuleViolationCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.PaymentRefundDeclined);
        payment.Status.Should().Be(PaymentStatus.Succeeded);
        await _paymentRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Anonymous_ThrowsUnauthorized()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => Handle(Guid.NewGuid(), "Reason", Guid.NewGuid());

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _paymentRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private Task<Elmanhg.Application.Payments.Shared.AdminPaymentResult> Handle(Guid paymentId, string reason, Guid key) => _handler.Handle(new RefundPaymentCommand(paymentId, reason, key), TestContext.Current.CancellationToken);

    private (Payment Payment, Subscription Subscription) AddSucceeded()
    {
        var payment = Payment.Create(_student.Id, SubscriptionPlan.Base, BillingPeriod.Monthly, 1, new Money(19900, "EGP"));
        var subscription = Subscription.Start(_student.Id, SubscriptionPlan.Base, BillingPeriod.Monthly, 1, PaidAt, "txn-1", _student.Id);
        payment.MarkSucceeded(subscription.Id, "txn-1", "{}", PaidAt);
        _payments.Add(payment);
        _subscriptions.Add(subscription);
        return (payment, subscription);
    }
}
