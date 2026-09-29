using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Payments.ResolvePaymentReview;
using Elmanhg.Application.Payments.Shared;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.SharedKernel;
using Elmanhg.Domain.Subscriptions;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Application.Features.Payments.ResolvePaymentReview;

public sealed class ResolvePaymentReviewHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
    private readonly IPaymentRepository _paymentRepository = Substitute.For<IPaymentRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly List<Payment> _payments = [];
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly ResolvePaymentReviewHandler _handler;

    public ResolvePaymentReviewHandlerTests()
    {
        _currentUserService.UserId.Returns(_adminId);
        _timeProvider.GetUtcNow().Returns(Now);
        _paymentRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<Payment, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Payment>, IQueryable<Payment>>?>(), Arg.Any<Func<IQueryable<Payment>, IOrderedQueryable<Payment>>?>(), Arg.Any<bool>())
            .Returns(call => _payments.FirstOrDefault(call.Arg<Expression<Func<Payment, bool>>>().Compile()));
        _handler = new ResolvePaymentReviewHandler(_paymentRepository, _userRepository, _currentUserService, _timeProvider);
    }

    [Fact]
    public async Task Handle_OpenReview_ResolvesAndSaves()
    {
        var payment = AddSucceeded();
        payment.FlagForReview(PaymentReviewReason.AskTeacherWithoutBase);

        var result = await Handle(payment.Id);

        (payment.ReviewResolvedBy, payment.ReviewResolvedAt, payment.Status).Should().Be(((Guid?)_adminId, (DateTimeOffset?)Now, PaymentStatus.Succeeded));
        (result.NeedsReview, result.ReviewResolvedAt).Should().Be((false, (DateTimeOffset?)Now));
        await _paymentRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoOpenReview_ThrowsReviewNotOpen()
    {
        var payment = AddSucceeded();

        var act = () => Handle(payment.Id);

        (await act.Should().ThrowAsync<BusinessRuleViolationCoreException>()).Which.ErrorCode.Should().Be(DomainErrorCodes.PaymentReviewNotOpen);
        await _paymentRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownPayment_ThrowsPaymentNotFound()
    {
        var act = () => Handle(Guid.NewGuid());

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.PaymentNotFound);
        await _paymentRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Anonymous_ThrowsUnauthorized()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => Handle(Guid.NewGuid());

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _paymentRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private Task<AdminPaymentResult> Handle(Guid paymentId) => _handler.Handle(new ResolvePaymentReviewCommand(paymentId), TestContext.Current.CancellationToken);

    private Payment AddSucceeded()
    {
        var payment = Payment.Create(Guid.NewGuid(), SubscriptionPlan.AskTeacher, BillingPeriod.Monthly, 1, new Money(9900, "EGP"));
        payment.MarkSucceeded(Guid.NewGuid(), "txn-1", "{}", Now.AddDays(-1));
        _payments.Add(payment);
        return payment;
    }
}
