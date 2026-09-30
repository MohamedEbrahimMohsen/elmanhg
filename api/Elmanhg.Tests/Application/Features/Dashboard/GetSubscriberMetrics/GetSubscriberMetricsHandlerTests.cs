using Elmanhg.Application.Dashboard.GetSubscriberMetrics;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.SharedKernel;
using Elmanhg.Domain.Subscriptions;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Linq.Expressions;

namespace Elmanhg.Tests.Application.Features.Dashboard.GetSubscriberMetrics;

public sealed class GetSubscriberMetricsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
    private readonly ISubscriptionRepository _subscriptionRepository = Substitute.For<ISubscriptionRepository>();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly GetSubscriberMetricsHandler _handler;

    public GetSubscriberMetricsHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(Now);
        _subscriptionRepository.CountActiveByPlanAsync(Arg.Any<CancellationToken>()).Returns([]);
        _handler = new GetSubscriberMetricsHandler(_subscriptionRepository, _timeProvider, Options.Create(new DashboardOptions()), Options.Create(new SubscriptionsOptions { Currency = "EGP" }));
    }

    [Fact]
    public async Task Handle_ZeroFillsPlansWithoutActiveSubscriptions()
    {
        _subscriptionRepository.CountActiveByPlanAsync(Arg.Any<CancellationToken>()).Returns([new PlanCount(SubscriptionPlan.Base, 11)]);

        var result = await _handler.Handle(new GetSubscriberMetricsQuery(null, null), TestContext.Current.CancellationToken);

        result.ActiveByPlan.Should().Equal(new PlanCountResult(SubscriptionPlan.Base, 11), new PlanCountResult(SubscriptionPlan.AskTeacher, 0));
        result.ActiveSubscriptions.Should().Be(11);
    }

    [Fact]
    public async Task Handle_ReturnsMrrInConfiguredCurrency()
    {
        _subscriptionRepository.GetMonthlyRecurringRevenueMinorAsync(Arg.Any<CancellationToken>()).Returns(12345L);

        var result = await _handler.Handle(new GetSubscriberMetricsQuery(null, null), TestContext.Current.CancellationToken);

        result.MonthlyRecurringRevenue.Should().Be(new Money(12345, "EGP"));
    }

    [Fact]
    public async Task Handle_ReturnsChurnInRangeAndThisMonth()
    {
        _subscriptionRepository.CountAsync(Arg.Any<CancellationToken>(), Arg.Any<Expression<Func<Subscription, bool>>>()).Returns(6, 2);

        var result = await _handler.Handle(new GetSubscriberMetricsQuery(null, null), TestContext.Current.CancellationToken);

        result.ChurnedInRange.Should().Be(6);
        result.ChurnedThisMonth.Should().Be(2);
        result.GeneratedAt.Should().Be(Now);
    }
}
