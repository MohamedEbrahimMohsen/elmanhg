using Elmanhg.Domain.Subscriptions;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Subscriptions;

public sealed class SubscriptionChurnSpecificationTests
{
    private static readonly DateTimeOffset Start = SubscriptionBuilder.DefaultStart;
    private static readonly DateTimeOffset PeriodEnd = Start.AddMonths(1);

    [Fact]
    public void ChurnedBetween_CancelledInWindow_Matches()
    {
        var subscription = new SubscriptionBuilder().InStatus(SubscriptionStatus.Cancelled).Build();

        var churned = SubscriptionChurnSpecification.ChurnedBetween(Start, Start.AddDays(2)).Compile()(subscription);

        churned.Should().BeTrue();
    }

    [Fact]
    public void ChurnedBetween_ExpiredWithoutCancelInWindow_Matches()
    {
        var subscription = new SubscriptionBuilder().InStatus(SubscriptionStatus.Expired).Build();

        var churned = SubscriptionChurnSpecification.ChurnedBetween(PeriodEnd.AddDays(-1), PeriodEnd.AddDays(1)).Compile()(subscription);

        churned.Should().BeTrue();
    }

    [Fact]
    public void ChurnedBetween_CancelledBeforeWindowExpiredInside_DoesNotMatch()
    {
        var subscription = new SubscriptionBuilder().InStatus(SubscriptionStatus.Cancelled).Build();
        subscription.Expire(PeriodEnd);

        var churned = SubscriptionChurnSpecification.ChurnedBetween(PeriodEnd.AddDays(-1), PeriodEnd.AddDays(1)).Compile()(subscription);

        churned.Should().BeFalse();
    }

    [Fact]
    public void ChurnedBetween_ActiveSubscription_DoesNotMatch()
    {
        var subscription = new SubscriptionBuilder().Build();

        var churned = SubscriptionChurnSpecification.ChurnedBetween(Start.AddYears(-1), Start.AddYears(1)).Compile()(subscription);

        churned.Should().BeFalse();
    }
}
