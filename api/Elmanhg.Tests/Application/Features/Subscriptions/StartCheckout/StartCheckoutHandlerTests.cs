using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.Payments;
using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Application.Subscriptions.StartCheckout;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.SharedKernel;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Linq.Expressions;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Application.Features.Subscriptions.StartCheckout;

public sealed class StartCheckoutHandlerTests
{
    private const string GatewayUrl = "https://gateway.test/checkout/1";
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly IPaymentRepository _paymentRepository = Substitute.For<IPaymentRepository>();
    private readonly ISubscriptionRepository _subscriptionRepository = Substitute.For<ISubscriptionRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IPaymentGateway _paymentGateway = Substitute.For<IPaymentGateway>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly List<Subscription> _subscriptions = [];
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly StartCheckoutHandler _handler;
    private Payment? _added;
    private PaymentCheckoutRequest? _gatewayRequest;

    public StartCheckoutHandlerTests()
    {
        _currentUserService.UserId.Returns(_studentId);
        _timeProvider.GetUtcNow().Returns(Now);
        _subscriptionRepository.FindAsync(Arg.Any<Expression<Func<Subscription, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subscription>, IQueryable<Subscription>>?>(), Arg.Any<Func<IQueryable<Subscription>, IOrderedQueryable<Subscription>>?>(), Arg.Any<bool>())
            .Returns(call => _subscriptions.Where(call.Arg<Expression<Func<Subscription, bool>>>().Compile()).ToList());
        _userRepository.GetByIdAsync(_studentId, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<User>, IQueryable<User>>?>(), Arg.Any<bool>()).Returns(User.CreateStudentWithPhone("Mona Ali", "01012345678"));
        _paymentGateway.StartCheckoutAsync(Arg.Do<PaymentCheckoutRequest>(x => _gatewayRequest = x), Arg.Any<CancellationToken>()).Returns(new PaymentCheckout(GatewayUrl));
        _paymentRepository.AddAsync(Arg.Do<Payment>(x => _added = x), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var options = new SubscriptionsOptions { AskTeacherMonthlyPriceMinor = 9900, BasePrices = { [BillingPeriod.Monthly] = new() { Months = 1, AmountMinor = 19900 } } };
        _handler = new StartCheckoutHandler(_paymentRepository, _subscriptionRepository, _userRepository, _paymentGateway, Microsoft.Extensions.Options.Options.Create(options), _timeProvider, _currentUserService);
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => Handle(SubscriptionPlan.Base, BillingPeriod.Monthly);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _paymentRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FreeStudentBaseMonthly_AddsPendingPaymentAtConfiguredPrice()
    {
        var result = await Handle(SubscriptionPlan.Base, BillingPeriod.Monthly);

        (_added!.Status, _added.Plan, _added.Period, _added.Amount, _added.StudentId).Should().Be((PaymentStatus.Pending, SubscriptionPlan.Base, BillingPeriod.Monthly, new Money(19900, "EGP"), _studentId));
        await _paymentRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        result.Amount.Should().Be(new Money(19900, "EGP"));
        result.PaymentId.Should().Be(_added.Id);
    }

    [Fact]
    public async Task Handle_FreeStudentBaseMonthly_ReturnsGatewayRedirectAndSendsCustomer()
    {
        var result = await Handle(SubscriptionPlan.Base, BillingPeriod.Monthly);

        result.RedirectUrl.Should().Be(GatewayUrl);
        (_gatewayRequest!.PaymentId, _gatewayRequest.Amount, _gatewayRequest.Customer).Should().Be((result.PaymentId, new Money(19900, "EGP"), new PaymentCustomer("Mona Ali", null, "01012345678")));
    }

    [Fact]
    public async Task Handle_EntitledBase_ThrowsCheckoutPlanAlreadyActive()
    {
        AddSubscription(SubscriptionPlan.Base);

        var act = () => Handle(SubscriptionPlan.Base, BillingPeriod.Monthly);

        (await act.Should().ThrowAsync<BusinessRuleViolationCoreException>()).Which.ErrorCode.Should().Be(DomainErrorCodes.CheckoutPlanAlreadyActive);
        await _paymentGateway.DidNotReceive().StartCheckoutAsync(Arg.Any<PaymentCheckoutRequest>(), Arg.Any<CancellationToken>());
        await _paymentRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AskTeacherWithoutBase_ThrowsCheckoutRequiresBase()
    {
        var act = () => Handle(SubscriptionPlan.AskTeacher, BillingPeriod.Monthly);

        (await act.Should().ThrowAsync<BusinessRuleViolationCoreException>()).Which.ErrorCode.Should().Be(DomainErrorCodes.CheckoutRequiresBase);
        await _paymentRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AskTeacherWithBase_AddsAskTeacherPaymentAtAddOnPrice()
    {
        AddSubscription(SubscriptionPlan.Base);

        await Handle(SubscriptionPlan.AskTeacher, BillingPeriod.Monthly);

        (_added!.Plan, _added.Period, _added.AmountMinor).Should().Be((SubscriptionPlan.AskTeacher, BillingPeriod.Monthly, 9900L));
        await _paymentRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PeriodUnavailable_ThrowsBadRequest()
    {
        AddSubscription(SubscriptionPlan.Base);

        var act = () => Handle(SubscriptionPlan.AskTeacher, BillingPeriod.Termly);

        (await act.Should().ThrowAsync<BadRequestCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.CheckoutPeriodUnavailable);
        await _paymentRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UserMissing_ThrowsUserNotFound()
    {
        _userRepository.GetByIdAsync(_studentId, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<User>, IQueryable<User>>?>(), Arg.Any<bool>()).Returns((User?)null);

        var act = () => Handle(SubscriptionPlan.Base, BillingPeriod.Monthly);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotFound);
        await _paymentRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_GatewayUnavailable_PropagatesAndSavesNothing()
    {
        _paymentGateway.StartCheckoutAsync(Arg.Any<PaymentCheckoutRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(new ServiceUnavailableCoreException(ErrorCodes.PaymentGatewayUnavailable));

        var act = () => Handle(SubscriptionPlan.Base, BillingPeriod.Monthly);

        (await act.Should().ThrowAsync<ServiceUnavailableCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.PaymentGatewayUnavailable);
        await _paymentRepository.DidNotReceive().AddAsync(Arg.Any<Payment>(), Arg.Any<CancellationToken>());
        await _paymentRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private Task<CheckoutResult> Handle(SubscriptionPlan plan, BillingPeriod period) => _handler.Handle(new StartCheckoutCommand(plan, period), TestContext.Current.CancellationToken);

    private void AddSubscription(SubscriptionPlan plan) => _subscriptions.Add(new SubscriptionBuilder().ForStudent(_studentId).WithPlan(plan).StartingAt(Now.AddDays(-5)).Build());
}
