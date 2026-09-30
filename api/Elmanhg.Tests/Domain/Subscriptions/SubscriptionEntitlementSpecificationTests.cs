using Elmanhg.Domain.Subscriptions;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Subscriptions;

public sealed class SubscriptionEntitlementSpecificationTests
{
    private static readonly TimeSpan Grace = TimeSpan.FromDays(3);
    private static readonly DateTimeOffset PeriodEnd = SubscriptionBuilder.DefaultStart.AddMonths(1);

    [Theory]
    [InlineData(SubscriptionStatus.Active, -1, true)]
    [InlineData(SubscriptionStatus.Active, 1, true)]
    [InlineData(SubscriptionStatus.Active, 4, false)]
    [InlineData(SubscriptionStatus.PastDue, -1, true)]
    [InlineData(SubscriptionStatus.PastDue, 1, true)]
    [InlineData(SubscriptionStatus.PastDue, 4, false)]
    [InlineData(SubscriptionStatus.Cancelled, -1, true)]
    [InlineData(SubscriptionStatus.Cancelled, 1, false)]
    [InlineData(SubscriptionStatus.Cancelled, 4, false)]
    [InlineData(SubscriptionStatus.Expired, -1, false)]
    [InlineData(SubscriptionStatus.Expired, 1, false)]
    [InlineData(SubscriptionStatus.Expired, 4, false)]
    public void EntitledFor_StatusAndTime_AgreesWithIsEntitledAt(SubscriptionStatus status, int daysAfterEnd, bool expected)
    {
        var subscription = new SubscriptionBuilder().InStatus(status).Build();
        var now = PeriodEnd.AddDays(daysAfterEnd);

        var satisfied = SubscriptionEntitlementSpecification.EntitledFor(subscription.StudentId, now, Grace).Compile()(subscription);

        (satisfied, subscription.IsEntitledAt(now, Grace)).Should().Be((expected, expected));
    }

    [Fact]
    public void EntitledFor_OtherStudent_IsNotSatisfied()
    {
        var subscription = new SubscriptionBuilder().Build();

        var satisfied = SubscriptionEntitlementSpecification.EntitledFor(Guid.NewGuid(), PeriodEnd.AddDays(-1), Grace).Compile()(subscription);

        satisfied.Should().BeFalse();
    }

    [Fact]
    public void EntitledForStudents_ListedStudentEntitled_IsSatisfied()
    {
        var subscription = new SubscriptionBuilder().Build();

        var satisfied = SubscriptionEntitlementSpecification.EntitledForStudents([Guid.NewGuid(), subscription.StudentId], PeriodEnd.AddDays(-1), Grace).Compile()(subscription);

        satisfied.Should().BeTrue();
    }

    [Fact]
    public void EntitledForStudents_UnlistedStudent_IsNotSatisfied()
    {
        var subscription = new SubscriptionBuilder().Build();

        var satisfied = SubscriptionEntitlementSpecification.EntitledForStudents([Guid.NewGuid()], PeriodEnd.AddDays(-1), Grace).Compile()(subscription);

        satisfied.Should().BeFalse();
    }
}
