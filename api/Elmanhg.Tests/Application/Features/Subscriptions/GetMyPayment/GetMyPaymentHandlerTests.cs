using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Subscriptions.GetMyPayment;
using Elmanhg.Domain.SharedKernel;
using Elmanhg.Domain.Subscriptions;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Subscriptions.GetMyPayment;

public sealed class GetMyPaymentHandlerTests
{
    private readonly IPaymentRepository _paymentRepository = Substitute.For<IPaymentRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly List<Payment> _payments = [];
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly GetMyPaymentHandler _handler;

    public GetMyPaymentHandlerTests()
    {
        _currentUserService.UserId.Returns(_studentId);
        _paymentRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<Payment, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Payment>, IQueryable<Payment>>?>(), Arg.Any<Func<IQueryable<Payment>, IOrderedQueryable<Payment>>?>(), Arg.Any<bool>())
            .Returns(call => _payments.FirstOrDefault(call.Arg<Expression<Func<Payment, bool>>>().Compile()));
        _handler = new GetMyPaymentHandler(_paymentRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new GetMyPaymentQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
    }

    [Fact]
    public async Task Handle_OwnPendingPayment_ReturnsPendingResult()
    {
        var payment = Add(_studentId);

        var result = await _handler.Handle(new GetMyPaymentQuery(payment.Id), TestContext.Current.CancellationToken);

        (result.Id, result.Status, result.Amount, result.CompletedAt).Should().Be((payment.Id, PaymentStatus.Pending, new Money(19900, "EGP"), (DateTimeOffset?)null));
    }

    [Fact]
    public async Task Handle_OtherStudentsPayment_ThrowsPaymentNotFound()
    {
        var payment = Add(Guid.NewGuid());

        var act = () => _handler.Handle(new GetMyPaymentQuery(payment.Id), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.PaymentNotFound);
    }

    private Payment Add(Guid studentId)
    {
        var payment = Payment.Create(studentId, SubscriptionPlan.Base, BillingPeriod.Monthly, 1, new Money(19900, "EGP"));
        _payments.Add(payment);
        return payment;
    }
}
