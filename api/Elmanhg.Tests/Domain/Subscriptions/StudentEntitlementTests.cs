using Core.Errors;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Subscriptions;

public sealed class StudentEntitlementTests
{
    private static readonly TimeSpan Grace = TimeSpan.FromDays(3);
    private static readonly DateTimeOffset Now = SubscriptionBuilder.DefaultStart.AddDays(10);
    private static readonly DateTimeOffset InsideRenewalWindow = SubscriptionBuilder.DefaultStart.AddMonths(1).AddDays(-3);
    private static readonly TimeSpan RenewalWindow = TimeSpan.FromDays(7);
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

    [Fact]
    public void EnsureCanPurchase_FreeStudentBase_DoesNotThrow()
    {
        var act = () => StudentEntitlement.Free.EnsureCanPurchase(SubscriptionPlan.Base, Now, RenewalWindow);

        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureCanPurchase_EntitledBase_ThrowsCheckoutPlanAlreadyActive()
    {
        var entitlement = StudentEntitlement.Resolve([Build(SubscriptionPlan.Base)], Now, Grace);

        var act = () => entitlement.EnsureCanPurchase(SubscriptionPlan.Base, Now, RenewalWindow);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.CheckoutPlanAlreadyActive);
    }

    [Fact]
    public void EnsureCanPurchase_AskTeacherWithoutBase_ThrowsCheckoutRequiresBase()
    {
        var act = () => StudentEntitlement.Free.EnsureCanPurchase(SubscriptionPlan.AskTeacher, Now, RenewalWindow);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.CheckoutRequiresBase);
    }

    [Fact]
    public void EnsureCanPurchase_AskTeacherWithBase_DoesNotThrow()
    {
        var entitlement = StudentEntitlement.Resolve([Build(SubscriptionPlan.Base)], Now, Grace);

        var act = () => entitlement.EnsureCanPurchase(SubscriptionPlan.AskTeacher, Now, RenewalWindow);

        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureCanPurchase_AskTeacherAlreadyEntitled_ThrowsCheckoutPlanAlreadyActive()
    {
        var entitlement = StudentEntitlement.Resolve([Build(SubscriptionPlan.Base), Build(SubscriptionPlan.AskTeacher)], Now, Grace);

        var act = () => entitlement.EnsureCanPurchase(SubscriptionPlan.AskTeacher, Now, RenewalWindow);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.CheckoutPlanAlreadyActive);
    }

    [Fact]
    public void EnsureCanPurchase_BaseInsideRenewalWindow_DoesNotThrow()
    {
        var entitlement = StudentEntitlement.Resolve([Build(SubscriptionPlan.Base)], InsideRenewalWindow, Grace);

        var act = () => entitlement.EnsureCanPurchase(SubscriptionPlan.Base, InsideRenewalWindow, RenewalWindow);

        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureCanPurchase_CancelledBaseInsideRenewalWindow_DoesNotThrow()
    {
        var cancelled = new SubscriptionBuilder().ForStudent(_studentId).InStatus(SubscriptionStatus.Cancelled).Build();
        var entitlement = StudentEntitlement.Resolve([cancelled], InsideRenewalWindow, Grace);

        var act = () => entitlement.EnsureCanPurchase(SubscriptionPlan.Base, InsideRenewalWindow, RenewalWindow);

        entitlement.BaseSubscription.Should().BeSameAs(cancelled);
        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureCanPurchase_AskTeacherInsideRenewalWindow_DoesNotThrow()
    {
        var entitlement = StudentEntitlement.Resolve([Build(SubscriptionPlan.Base), Build(SubscriptionPlan.AskTeacher)], InsideRenewalWindow, Grace);

        var act = () => entitlement.EnsureCanPurchase(SubscriptionPlan.AskTeacher, InsideRenewalWindow, RenewalWindow);

        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureCanGrant_BaseWhenFree_DoesNotThrow()
    {
        var act = () => StudentEntitlement.Free.EnsureCanGrant(SubscriptionPlan.Base);

        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureCanGrant_AskTeacherWithoutBase_ThrowsComplimentaryRequiresBase()
    {
        var act = () => StudentEntitlement.Free.EnsureCanGrant(SubscriptionPlan.AskTeacher);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.ComplimentaryRequiresBase);
    }

    [Fact]
    public void EnsureCanGrant_HeldBase_ThrowsComplimentaryPlanAlreadyActive()
    {
        var entitlement = StudentEntitlement.Resolve([Build(SubscriptionPlan.Base)], Now, Grace);

        var act = () => entitlement.EnsureCanGrant(SubscriptionPlan.Base);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.ComplimentaryPlanAlreadyActive);
    }

    [Fact]
    public void EnsureCanGrant_AskTeacherWithBase_DoesNotThrow()
    {
        var entitlement = StudentEntitlement.Resolve([Build(SubscriptionPlan.Base)], Now, Grace);

        var act = () => entitlement.EnsureCanGrant(SubscriptionPlan.AskTeacher);

        act.Should().NotThrow();
    }

    private Subscription Build(SubscriptionPlan plan) => new SubscriptionBuilder().ForStudent(_studentId).WithPlan(plan).Build();
}
