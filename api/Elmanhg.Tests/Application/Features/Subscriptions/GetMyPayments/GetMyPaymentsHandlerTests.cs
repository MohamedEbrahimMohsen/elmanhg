using Core.DDD.Models;
using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Subscriptions.GetMyPayments;
using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Domain.SharedKernel;
using Elmanhg.Domain.Subscriptions;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Subscriptions.GetMyPayments;

public sealed class GetMyPaymentsHandlerTests
{
    private static readonly DateTimeOffset CompletedAt = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);
    private readonly IPaymentRepository _paymentRepository = Substitute.For<IPaymentRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly List<Payment> _payments = [];
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly GetMyPaymentsHandler _handler;

    public GetMyPaymentsHandlerTests()
    {
        _currentUserService.UserId.Returns(_studentId);
        _paymentRepository.FindPaginatedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<Payment, bool>>?>(), Arg.Any<Func<IQueryable<Payment>, IQueryable<Payment>>?>(), Arg.Any<Func<IQueryable<Payment>, IOrderedQueryable<Payment>>?>(), Arg.Any<bool>())
            .Returns(call =>
            {
                var items = _payments.Where(call.Arg<Expression<Func<Payment, bool>>?>()!.Compile()).ToList();
                return new PageData<Payment> { Items = items, PageNumber = call.ArgAt<int>(0), PageSize = call.ArgAt<int>(1), TotalItems = items.Count, TotalPages = 1 };
            });
        _handler = new GetMyPaymentsHandler(_paymentRepository, _currentUserService);
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new GetMyPaymentsQuery(), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
    }

    [Fact]
    public async Task Handle_MixedPayments_ReturnsOwnCompletedPaymentsOnly()
    {
        var succeeded = Add(_studentId, PaymentStatus.Succeeded);
        var failed = Add(_studentId, PaymentStatus.Failed);
        Add(_studentId, PaymentStatus.Pending);
        Add(Guid.NewGuid(), PaymentStatus.Succeeded);

        var result = await _handler.Handle(new GetMyPaymentsQuery(), TestContext.Current.CancellationToken);

        result.Items.Select(x => x.Id).Should().BeEquivalentTo([succeeded.Id, failed.Id]);
        result.TotalItems.Should().Be(2);
    }

    [Fact]
    public async Task Handle_SucceededPayment_MapsAmountPlanPeriodStatusAndDates()
    {
        var payment = Add(_studentId, PaymentStatus.Succeeded);

        var result = await _handler.Handle(new GetMyPaymentsQuery(), TestContext.Current.CancellationToken);

        result.Items.Should().Equal(new PaymentResult(payment.Id, SubscriptionPlan.Base, BillingPeriod.Termly, new Money(69900, "EGP"), PaymentStatus.Succeeded, payment.CreationDate, CompletedAt));
    }

    private Payment Add(Guid studentId, PaymentStatus status)
    {
        var payment = Payment.Create(studentId, SubscriptionPlan.Base, BillingPeriod.Termly, 4, new Money(69900, "EGP"));
        if (status == PaymentStatus.Succeeded)
        {
            payment.MarkSucceeded(Guid.NewGuid(), $"txn-{Guid.NewGuid():N}", "{}", CompletedAt);
        }
        else if (status == PaymentStatus.Failed)
        {
            payment.MarkFailed($"txn-{Guid.NewGuid():N}", "{}", CompletedAt);
        }

        _payments.Add(payment);
        return payment;
    }
}
