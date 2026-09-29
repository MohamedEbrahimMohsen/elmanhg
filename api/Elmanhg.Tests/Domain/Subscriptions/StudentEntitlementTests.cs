using Elmanhg.Domain.Subscriptions;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Subscriptions;

public sealed class StudentEntitlementTests
{
    private static readonly TimeSpan Grace = TimeSpan.FromDays(3);
    private static readonly DateTimeOffset Now = SubscriptionBuilder.DefaultStart.AddDays(10);
    private readonly Guid _studentId = Guid.NewGuid();

    [Fact]
    public void Resolve_NoSubscriptions_IsFree()
    {
        var entitlement = StudentEntitlement.Resolve([], Now, Grace);

        (entitlement.Tier, entitlement.HasAskTeacher, entitlement.BaseSubscription, entitlement.AskTeacherSubscription).Should().Be((PlanTier.Free, false, (Subscription?)null, (Subscription?)null));
    }

    [Fact]
    public void Resolve_EntitledBase_IsBaseTier()
    {
        var baseSubscription = Build(SubscriptionPlan.Base);

        var entitlement = StudentEntitlement.Resolve([baseSubscription], Now, Grace);

        entitlement.Tier.Should().Be(PlanTier.Base);
        entitlement.BaseSubscription.Should().BeSameAs(baseSubscription);
    }

    [Fact]
    public void Resolve_AskTeacherWithoutEntitledBase_HasNoAskTeacher()
    {
        var entitlement = StudentEntitlement.Resolve([Build(SubscriptionPlan.AskTeacher)], Now, Grace);

        (entitlement.Tier, entitlement.HasAskTeacher).Should().Be((PlanTier.Free, false));
    }

    [Fact]
    public void Resolve_BaseAndAskTeacher_HasAskTeacher()
    {
        var entitlement = StudentEntitlement.Resolve([Build(SubscriptionPlan.Base), Build(SubscriptionPlan.AskTeacher)], Now, Grace);

        entitlement.HasAskTeacher.Should().BeTrue();
    }

    [Fact]
    public void Resolve_LapsedBase_IsFree()
    {
        var lapsed = Build(SubscriptionPlan.Base);

        var entitlement = StudentEntitlement.Resolve([lapsed], lapsed.CurrentPeriodEnd.AddDays(4), Grace);

        entitlement.Tier.Should().Be(PlanTier.Free);
    }

    [Fact]
    public void Resolve_TwoEntitledBase_PicksLatestEntitledUntil()
    {
        var monthly = Build(SubscriptionPlan.Base);
        var yearly = new SubscriptionBuilder().ForStudent(_studentId).WithPeriod(BillingPeriod.Yearly, 12).Build();

        var entitlement = StudentEntitlement.Resolve([monthly, yearly], Now, Grace);

        entitlement.BaseSubscription.Should().BeSameAs(yearly);
    }

    private Subscription Build(SubscriptionPlan plan) => new SubscriptionBuilder().ForStudent(_studentId).WithPlan(plan).Build();
}
