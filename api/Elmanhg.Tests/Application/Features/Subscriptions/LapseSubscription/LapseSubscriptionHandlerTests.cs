using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Subscriptions.LapseSubscription;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Tests.Builders;
using FluentAssertions;
using NSubstitute;

namespace Elmanhg.Tests.Application.Features.Subscriptions.LapseSubscription;

public sealed class LapseSubscriptionHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly ISubscriptionRepository _subscriptionRepository = Substitute.For<ISubscriptionRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly LapseSubscriptionHandler _handler;

    public LapseSubscriptionHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(Now);
        _handler = new LapseSubscriptionHandler(_subscriptionRepository, Microsoft.Extensions.Options.Options.Create(new SubscriptionsOptions()), _timeProvider);
    }

    [Fact]
    public async Task Handle_Missing_SavesNothing()
    {
        await Handle(Guid.NewGuid());

        await _subscriptionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NotDue_SavesNothing()
    {
        var subscription = Stored(SubscriptionStatus.Active, Now.AddDays(-5));

        await Handle(subscription.Id);

        subscription.Status.Should().Be(SubscriptionStatus.Active);
        await _subscriptionRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ActivePastEnd_MarksPastDueAndSaves()
    {
        var subscription = Stored(SubscriptionStatus.Active, Now.AddMonths(-1).AddDays(-1));

        await Handle(subscription.Id);

        subscription.Status.Should().Be(SubscriptionStatus.PastDue);
        await _subscriptionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PastDuePastGrace_ExpiresAndSaves()
    {
        var subscription = Stored(SubscriptionStatus.PastDue, Now.AddMonths(-1).AddDays(-10));

        await Handle(subscription.Id);

        (subscription.Status, subscription.ExpiredAt).Should().Be((SubscriptionStatus.Expired, (DateTimeOffset?)(subscription.CurrentPeriodEnd + TimeSpan.FromDays(3))));
        await _subscriptionRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private Task Handle(Guid subscriptionId) => _handler.Handle(new LapseSubscriptionCommand(subscriptionId), TestContext.Current.CancellationToken);

    private Subscription Stored(SubscriptionStatus status, DateTimeOffset start)
    {
        var subscription = new SubscriptionBuilder().InStatus(status).StartingAt(start).Build();
        _subscriptionRepository.GetByIdAsync(subscription.Id, Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Subscription>, IQueryable<Subscription>>?>(), Arg.Any<bool>()).Returns(subscription);
        return subscription;
    }
}
