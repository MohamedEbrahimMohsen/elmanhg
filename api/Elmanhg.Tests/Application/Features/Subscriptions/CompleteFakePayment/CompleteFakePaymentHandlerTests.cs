using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.Payments;
using Elmanhg.Application.Subscriptions.CompleteFakePayment;
using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Domain.SharedKernel;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Tests.Builders;
using Elmanhg.Tests.Integration.Subscriptions;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Application.Features.Subscriptions.CompleteFakePayment;

public sealed class CompleteFakePaymentHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly IPaymentRepository _paymentRepository = Substitute.For<IPaymentRepository>();
    private readonly ISubscriptionRepository _subscriptionRepository = Substitute.For<ISubscriptionRepository>();
    private readonly IPaymentGateway _paymentGateway = Substitute.For<IPaymentGateway>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly List<Payment> _payments = [];
    private readonly List<Subscription> _subscriptions = [];
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly CompleteFakePaymentHandler _handler;
    private Subscription? _addedSubscription;

    public CompleteFakePaymentHandlerTests()
    {
        _currentUserService.UserId.Returns(_studentId);
        _timeProvider.GetUtcNow().Returns(Now);
        _paymentGateway.SupportsSimulatedCompletion.Returns(true);
        _paymentRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<Payment, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Payment>, IQueryable<Payment>>?>(), Arg.Any<Func<IQueryable<Payment>, IOrderedQueryable<Payment>>?>(), Arg.Any<bool>())
            .Returns(call => _payments.FirstOrDefault(call.Arg<Expression<Func<Payment, bool>>>().Compile()));
        _subscriptionRepository.FindAsync(Arg.Any<Expression<Func<Subscription, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subscription>, IQueryable<Subscription>>?>(), Arg.Any<Func<IQueryable<Subscription>, IOrderedQueryable<Subscription>>?>(), Arg.Any<bool>())
            .Returns(call => _subscriptions.Where(call.Arg<Expression<Func<Subscription, bool>>>().Compile()).ToList());
        _subscriptionRepository.AddAsync(Arg.Do<Subscription>(x => _addedSubscription = x), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var options = new SubscriptionsOptions { AskTeacherMonthlyPriceMinor = 9900, BasePrices = { [BillingPeriod.Monthly] = new() { Months = 1, AmountMinor = 19900 } } };
        _handler = new CompleteFakePaymentHandler(_paymentRepository, _subscriptionRepository, _paymentGateway, Microsoft.Extensions.Options.Options.Create(options), _timeProvider, _currentUserService);
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => Handle(Guid.NewGuid(), true);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _paymentRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_GatewayCannotSimulate_ThrowsFakeCheckoutUnavailable()
    {
        _paymentGateway.SupportsSimulatedCompletion.Returns(false);
        var payment = AddPending(_studentId);

        var act = () => Handle(payment.Id, true);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.FakeCheckoutUnavailable);
        await _paymentRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PaymentNotOwned_ThrowsPaymentNotFound()
    {
        var payment = AddPending(Guid.NewGuid());

        var act = () => Handle(payment.Id, true);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.PaymentNotFound);
        await _paymentRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Succeeded_AddsSubscriptionAndReturnsSucceeded()
    {
        var payment = AddPending(_studentId);

        var result = await Handle(payment.Id, true);

        (_addedSubscription!.StudentId, _addedSubscription.Plan, _addedSubscription.Period, _addedSubscription.CurrentPeriodEnd).Should().Be((_studentId, SubscriptionPlan.Base, BillingPeriod.Monthly, Now.AddMonths(1)));
        result.Status.Should().Be(PaymentStatus.Succeeded);
        payment.PaymobTransactionId.Should().Be("fake-" + payment.Id.ToString("N"));
        await _paymentRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Failed_ReturnsFailedWithoutSubscription()
    {
        var payment = AddPending(_studentId);

        var result = await Handle(payment.Id, false);

        result.Status.Should().Be(PaymentStatus.Failed);
        await _subscriptionRepository.DidNotReceive().AddAsync(Arg.Any<Subscription>(), Arg.Any<CancellationToken>());
        await _paymentRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SucceededWhileBaseEntitled_RenewsHeldSubscription()
    {
        var held = new SubscriptionBuilder().ForStudent(_studentId).WithPlan(SubscriptionPlan.Base).StartingAt(Now.AddDays(-5)).Build();
        var previousEnd = held.CurrentPeriodEnd;
        _subscriptions.Add(held);
        var payment = AddPending(_studentId);

        var result = await Handle(payment.Id, true);

        (result.Status, payment.SubscriptionId, held.CurrentPeriodEnd).Should().Be((PaymentStatus.Succeeded, (Guid?)held.Id, previousEnd.AddMonths(1)));
        await _subscriptionRepository.DidNotReceive().AddAsync(Arg.Any<Subscription>(), Arg.Any<CancellationToken>());
        await _paymentRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PaymentAlreadyCompleted_ThrowsPaymentNotPending()
    {
        var payment = AddPending(_studentId);
        payment.MarkFailed("txn-1", "{}", Now);

        var act = () => Handle(payment.Id, true);

        (await act.Should().ThrowAsync<BusinessRuleViolationCoreException>()).Which.ErrorCode.Should().Be(DomainErrorCodes.PaymentNotPending);
        await _paymentRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PeriodNoLongerConfigured_SettlesWithSnapshotMonths()
    {
        var payment = AddPending(_studentId, BillingPeriod.Yearly);

        var result = await Handle(payment.Id, true);

        (result.Status, _addedSubscription!.Period, _addedSubscription.CurrentPeriodEnd).Should().Be((PaymentStatus.Succeeded, BillingPeriod.Yearly, Now.AddMonths(12)));
        await _paymentRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FailedPaymentSucceeded_ThrowsPaymentNotPending()
    {
        var payment = AddPending(_studentId);
        payment.MarkFailed("txn-1", "{}", Now);

        var act = () => Handle(payment.Id, true);

        (await act.Should().ThrowAsync<BusinessRuleViolationCoreException>()).Which.ErrorCode.Should().Be(DomainErrorCodes.PaymentNotPending);
        (payment.Status, payment.PaymobTransactionId).Should().Be((PaymentStatus.Failed, "txn-1"));
        await _subscriptionRepository.DidNotReceive().AddAsync(Arg.Any<Subscription>(), Arg.Any<CancellationToken>());
        await _paymentRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private Task<PaymentResult> Handle(Guid paymentId, bool succeeded) => _handler.Handle(new CompleteFakePaymentCommand(paymentId, succeeded), TestContext.Current.CancellationToken);

    private Payment AddPending(Guid studentId, BillingPeriod period = BillingPeriod.Monthly)
    {
        var payment = Payment.Create(studentId, SubscriptionPlan.Base, period, SubscriptionTestData.MonthsFor(period), new Money(19900, "EGP"));
        _payments.Add(payment);
        return payment;
    }
}
