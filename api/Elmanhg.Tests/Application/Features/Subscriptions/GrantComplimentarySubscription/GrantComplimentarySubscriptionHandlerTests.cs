using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Subscriptions.GrantComplimentarySubscription;
using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Tests.Application.Features.Students;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Application.Features.Subscriptions.GrantComplimentarySubscription;

public sealed class GrantComplimentarySubscriptionHandlerTests
{
    private static readonly DateTimeOffset Now = SubscriptionBuilder.DefaultStart.AddDays(10);

    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly ISubscriptionRepository _subscriptionRepository = Substitute.For<ISubscriptionRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly User _student = User.CreateStudentWithPhone("Mona", "01012345678");
    private readonly GrantComplimentarySubscriptionHandler _handler;

    public GrantComplimentarySubscriptionHandlerTests()
    {
        _currentUserService.UserId.Returns(_adminId);
        _timeProvider.GetUtcNow().Returns(Now);
        StudentRepositoryStub.Stub(_userRepository, _student);
        SubscriptionRepositoryStub.Stub(_subscriptionRepository);
        var options = new SubscriptionsOptions
        {
            GracePeriodDays = 3,
            AskTeacherMonthlyPriceMinor = 9900,
            BasePrices = new() { [BillingPeriod.Monthly] = new PlanPriceOptions { Months = 1, AmountMinor = 19900 }, [BillingPeriod.Termly] = new PlanPriceOptions { Months = 4, AmountMinor = 69900 } },
        };
        _handler = new GrantComplimentarySubscriptionHandler(_userRepository, _subscriptionRepository, _currentUserService, Options.Create(options), _timeProvider);
    }

    [Fact]
    public async Task Handle_TermlyBaseForFreeStudent_StartsComplimentarySubscription()
    {
        var result = await _handler.Handle(new GrantComplimentarySubscriptionCommand(_student.Id, SubscriptionPlan.Base, BillingPeriod.Termly), TestContext.Current.CancellationToken);

        await _subscriptionRepository.Received(1).AddAsync(Arg.Is<Subscription>(x => x.StudentId == _student.Id && x.Plan == SubscriptionPlan.Base && x.Period == BillingPeriod.Termly && x.PaymobReference == null && x.CreatedBy == _adminId && x.CurrentPeriodStart == Now && x.CurrentPeriodEnd == Now.AddMonths(4)), Arg.Any<CancellationToken>());
        await _subscriptionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        (result.IsComplimentary, result.Status, result.CurrentPeriodEnd).Should().Be((true, SubscriptionStatus.Active, Now.AddMonths(4)));
    }

    [Fact]
    public async Task Handle_AskTeacherWithoutBase_ThrowsComplimentaryRequiresBase()
    {
        var act = () => _handler.Handle(new GrantComplimentarySubscriptionCommand(_student.Id, SubscriptionPlan.AskTeacher, BillingPeriod.Monthly), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BusinessRuleViolationCoreException>()).Which.ErrorCode.Should().Be(DomainErrorCodes.ComplimentaryRequiresBase);
        await _subscriptionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_BaseAlreadyHeld_ThrowsComplimentaryPlanAlreadyActive()
    {
        SubscriptionRepositoryStub.Stub(_subscriptionRepository, SubscriptionRepositoryStub.EntitledBase(_student.Id, Now));

        var act = () => _handler.Handle(new GrantComplimentarySubscriptionCommand(_student.Id, SubscriptionPlan.Base, BillingPeriod.Monthly), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BusinessRuleViolationCoreException>()).Which.ErrorCode.Should().Be(DomainErrorCodes.ComplimentaryPlanAlreadyActive);
        await _subscriptionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AskTeacherTermly_ThrowsComplimentaryPeriodUnavailable()
    {
        SubscriptionRepositoryStub.Stub(_subscriptionRepository, SubscriptionRepositoryStub.EntitledBase(_student.Id, Now));

        var act = () => _handler.Handle(new GrantComplimentarySubscriptionCommand(_student.Id, SubscriptionPlan.AskTeacher, BillingPeriod.Termly), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<BadRequestCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.ComplimentaryPeriodUnavailable);
        await _subscriptionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownStudent_ThrowsStudentNotFound()
    {
        var act = () => _handler.Handle(new GrantComplimentarySubscriptionCommand(Guid.NewGuid(), SubscriptionPlan.Base, BillingPeriod.Monthly), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.StudentNotFound);
        await _subscriptionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => _handler.Handle(new GrantComplimentarySubscriptionCommand(_student.Id, SubscriptionPlan.Base, BillingPeriod.Monthly), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _subscriptionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
