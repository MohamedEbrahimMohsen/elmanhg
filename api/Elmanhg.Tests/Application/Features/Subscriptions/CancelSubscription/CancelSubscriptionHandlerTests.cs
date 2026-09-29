using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Subscriptions.CancelSubscription;
using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;
using System.Linq.Expressions;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Application.Features.Subscriptions.CancelSubscription;

public sealed class CancelSubscriptionHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly ISubscriptionRepository _subscriptionRepository = Substitute.For<ISubscriptionRepository>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly List<Subscription> _subscriptions = [];
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly CancelSubscriptionHandler _handler;

    public CancelSubscriptionHandlerTests()
    {
        _currentUserService.UserId.Returns(_studentId);
        _timeProvider.GetUtcNow().Returns(Now);
        _subscriptionRepository.FirstOrDefaultAsync(Arg.Any<Expression<Func<Subscription, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subscription>, IQueryable<Subscription>>?>(), Arg.Any<Func<IQueryable<Subscription>, IOrderedQueryable<Subscription>>?>(), Arg.Any<bool>())
            .Returns(call => _subscriptions.FirstOrDefault(call.Arg<Expression<Func<Subscription, bool>>>().Compile()));
        _subscriptionRepository.FindAsync(Arg.Any<Expression<Func<Subscription, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subscription>, IQueryable<Subscription>>?>(), Arg.Any<Func<IQueryable<Subscription>, IOrderedQueryable<Subscription>>?>(), Arg.Any<bool>())
            .Returns(call => _subscriptions.Where(call.Arg<Expression<Func<Subscription, bool>>>().Compile()).ToList());
        _handler = new CancelSubscriptionHandler(_subscriptionRepository, Microsoft.Extensions.Options.Options.Create(new SubscriptionsOptions()), _timeProvider, _currentUserService);
    }

    [Fact]
    public async Task Handle_NoCurrentUser_ThrowsUserNotAuthenticated()
    {
        _currentUserService.UserId.Returns((Guid?)null);

        var act = () => Handle(Guid.NewGuid());

        (await act.Should().ThrowAsync<UnauthorizedCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.UserNotAuthenticated);
        await _subscriptionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OtherStudentsSubscription_ThrowsSubscriptionNotFound()
    {
        var subscription = Add(Guid.NewGuid(), SubscriptionStatus.Active, Now.AddDays(-5));

        var act = () => Handle(subscription.Id);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be(ErrorCodes.SubscriptionNotFound);
        subscription.Status.Should().Be(SubscriptionStatus.Active);
        await _subscriptionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ActiveBase_CancelsAndReturnsEntitlementStillBase()
    {
        var subscription = Add(_studentId, SubscriptionStatus.Active, Now.AddDays(-5));

        var result = await Handle(subscription.Id);

        (subscription.Status, subscription.CancelledAt).Should().Be((SubscriptionStatus.Cancelled, (DateTimeOffset?)Now));
        (result.Tier, result.Subscriptions.Single().Status, result.Subscriptions.Single().EntitledUntil).Should().Be((PlanTier.Base, SubscriptionStatus.Cancelled, subscription.CurrentPeriodEnd));
        await _subscriptionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AlreadyCancelled_ThrowsSubscriptionEnded()
    {
        var subscription = Add(_studentId, SubscriptionStatus.Cancelled, Now.AddDays(-5));

        var act = () => Handle(subscription.Id);

        (await act.Should().ThrowAsync<BusinessRuleViolationCoreException>()).Which.ErrorCode.Should().Be(DomainErrorCodes.SubscriptionEnded);
        await _subscriptionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ActiveLapsedPastGrace_ThrowsSubscriptionEnded()
    {
        var subscription = Add(_studentId, SubscriptionStatus.Active, Now.AddMonths(-1).AddDays(-4));

        var act = () => Handle(subscription.Id);

        (await act.Should().ThrowAsync<BusinessRuleViolationCoreException>()).Which.ErrorCode.Should().Be(DomainErrorCodes.SubscriptionEnded);
        subscription.Status.Should().Be(SubscriptionStatus.Active);
        await _subscriptionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private Task<EntitlementResult> Handle(Guid subscriptionId) => _handler.Handle(new CancelSubscriptionCommand(subscriptionId), TestContext.Current.CancellationToken);

    private Subscription Add(Guid studentId, SubscriptionStatus status, DateTimeOffset start)
    {
        var subscription = new SubscriptionBuilder().ForStudent(studentId).InStatus(status).StartingAt(start).Build();
        _subscriptions.Add(subscription);
        return subscription;
    }
}
