using Core.Errors;
using Elmanhg.Domain.SharedKernel.Exceptions;
using Elmanhg.Domain.Subscriptions;
using Elmanhg.Tests.Builders;
using FluentAssertions;

namespace Elmanhg.Tests.Domain.Subscriptions;

public sealed class SubscriptionTests
{
    private static readonly DateTimeOffset Start = SubscriptionBuilder.DefaultStart;
    private static readonly DateTimeOffset MonthEnd = Start.AddMonths(1);
    private static readonly DateTimeOffset MidPeriod = Start.AddDays(10);
    private static readonly TimeSpan Grace = TimeSpan.FromDays(3);
    private static readonly TimeSpan RenewalWindow = TimeSpan.FromDays(7);

    [Fact]
    public void Start_ValidInput_IsActiveForOnePeriod()
    {
        var studentId = Guid.NewGuid();

        var subscription = Subscription.Start(studentId, SubscriptionPlan.Base, BillingPeriod.Termly, 4, Start, "ref-7", studentId);

        (subscription.Status, subscription.CurrentPeriodStart, subscription.CurrentPeriodEnd).Should().Be((SubscriptionStatus.Active, Start, Start.AddMonths(4)));
        (subscription.StudentId, subscription.Plan, subscription.Period, subscription.PaymobReference).Should().Be((studentId, SubscriptionPlan.Base, BillingPeriod.Termly, "ref-7"));
    }

    [Fact]
    public void Start_PeriodMonthsBelowOne_ThrowsPeriodInvalid()
    {
        var act = () => Subscription.Start(Guid.NewGuid(), SubscriptionPlan.Base, BillingPeriod.Monthly, 0, Start, null, null);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.SubscriptionPeriodInvalid);
    }

    [Fact]
    public void Renew_Active_ExtendsFromPreviousPeriodEnd()
    {
        var subscription = new SubscriptionBuilder().Build();

        subscription.Renew(BillingPeriod.Monthly, 4, "ref-2", MidPeriod, Grace);

        (subscription.CurrentPeriodStart, subscription.CurrentPeriodEnd, subscription.Status).Should().Be((MonthEnd, MonthEnd.AddMonths(4), SubscriptionStatus.Active));
    }

    [Fact]
    public void Renew_PastDue_ReactivatesAndExtends()
    {
        var subscription = new SubscriptionBuilder().InStatus(SubscriptionStatus.PastDue).Build();

        subscription.Renew(BillingPeriod.Monthly, 1, null, MonthEnd.AddDays(1), Grace);

        (subscription.Status, subscription.CurrentPeriodEnd).Should().Be((SubscriptionStatus.Active, MonthEnd.AddMonths(1)));
    }

    [Fact]
    public void Renew_Expired_ThrowsSubscriptionEnded()
    {
        var subscription = new SubscriptionBuilder().InStatus(SubscriptionStatus.Expired).Build();

        var act = () => subscription.Renew(BillingPeriod.Monthly, 1, null, MidPeriod, Grace);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.SubscriptionEnded);
        (subscription.CurrentPeriodStart, subscription.CurrentPeriodEnd).Should().Be((Start, MonthEnd));
    }

    [Fact]
    public void Renew_CancelledBeforeEnd_ResumesAndClearsCancelledAt()
    {
        var subscription = new SubscriptionBuilder().InStatus(SubscriptionStatus.Cancelled).Build();

        subscription.Renew(BillingPeriod.Monthly, 1, null, MidPeriod, Grace);

        (subscription.Status, subscription.CancelledAt, subscription.CurrentPeriodEnd).Should().Be((SubscriptionStatus.Active, (DateTimeOffset?)null, MonthEnd.AddMonths(1)));
    }

    [Fact]
    public void Renew_ActivePastGrace_ThrowsSubscriptionEnded()
    {
        var subscription = new SubscriptionBuilder().Build();

        var act = () => subscription.Renew(BillingPeriod.Monthly, 1, null, MonthEnd + Grace, Grace);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.SubscriptionEnded);
        subscription.CurrentPeriodEnd.Should().Be(MonthEnd);
    }

    [Fact]
    public void Renew_InsideGrace_ExtendsFromPreviousPeriodEnd()
    {
        var subscription = new SubscriptionBuilder().Build();

        subscription.Renew(BillingPeriod.Monthly, 1, null, MonthEnd.AddDays(2), Grace);

        (subscription.CurrentPeriodStart, subscription.CurrentPeriodEnd).Should().Be((MonthEnd, MonthEnd.AddMonths(1)));
    }

    [Fact]
    public void Renew_DifferentPeriod_SwitchesPeriod()
    {
        var subscription = new SubscriptionBuilder().Build();

        subscription.Renew(BillingPeriod.Yearly, 12, null, MidPeriod, Grace);

        (subscription.Period, subscription.CurrentPeriodEnd).Should().Be((BillingPeriod.Yearly, MonthEnd.AddMonths(12)));
    }

    [Fact]
    public void Renew_PeriodMonthsBelowOne_ThrowsPeriodInvalid()
    {
        var subscription = new SubscriptionBuilder().Build();

        var act = () => subscription.Renew(BillingPeriod.Monthly, 0, null, MidPeriod, Grace);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.SubscriptionPeriodInvalid);
        subscription.CurrentPeriodEnd.Should().Be(MonthEnd);
    }

    [Fact]
    public void Renew_WithoutReference_KeepsPreviousReference()
    {
        var subscription = new SubscriptionBuilder().Build();

        subscription.Renew(BillingPeriod.Monthly, 1, null, MidPeriod, Grace);

        subscription.PaymobReference.Should().Be(SubscriptionBuilder.Reference);
    }

    [Theory]
    [InlineData(8, false)]
    [InlineData(7, true)]
    [InlineData(1, true)]
    [InlineData(-1, true)]
    public void IsRenewableAt_ByDaysBeforeEnd_OpensInsideWindow(int daysBeforeEnd, bool expected)
    {
        var subscription = new SubscriptionBuilder().Build();

        var renewable = subscription.IsRenewableAt(MonthEnd.AddDays(-daysBeforeEnd), RenewalWindow);

        renewable.Should().Be(expected);
    }

    [Fact]
    public void MarkPastDue_Active_BecomesPastDue()
    {
        var subscription = new SubscriptionBuilder().Build();

        subscription.MarkPastDue();

        subscription.Status.Should().Be(SubscriptionStatus.PastDue);
    }

    [Theory]
    [InlineData(SubscriptionStatus.PastDue)]
    [InlineData(SubscriptionStatus.Cancelled)]
    [InlineData(SubscriptionStatus.Expired)]
    public void MarkPastDue_NotActive_ThrowsNotActive(SubscriptionStatus status)
    {
        var subscription = new SubscriptionBuilder().InStatus(status).Build();

        var act = subscription.MarkPastDue;

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.SubscriptionNotActive);
    }

    [Theory]
    [InlineData(SubscriptionStatus.Active)]
    [InlineData(SubscriptionStatus.PastDue)]
    public void Cancel_ActiveOrPastDue_BecomesCancelledWithTimestamp(SubscriptionStatus status)
    {
        var subscription = new SubscriptionBuilder().InStatus(status).Build();
        var cancelledAt = Start.AddDays(10);

        subscription.Cancel(cancelledAt);

        (subscription.Status, subscription.CancelledAt).Should().Be((SubscriptionStatus.Cancelled, (DateTimeOffset?)cancelledAt));
    }

    [Theory]
    [InlineData(SubscriptionStatus.Cancelled)]
    [InlineData(SubscriptionStatus.Expired)]
    public void Cancel_Ended_ThrowsSubscriptionEnded(SubscriptionStatus status)
    {
        var subscription = new SubscriptionBuilder().InStatus(status).Build();

        var act = () => subscription.Cancel(Start.AddDays(10));

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.SubscriptionEnded);
    }

    [Theory]
    [InlineData(SubscriptionStatus.Active)]
    [InlineData(SubscriptionStatus.PastDue)]
    [InlineData(SubscriptionStatus.Cancelled)]
    public void Expire_NotExpired_BecomesExpiredWithTimestamp(SubscriptionStatus status)
    {
        var subscription = new SubscriptionBuilder().InStatus(status).Build();
        var expiredAt = MonthEnd.AddDays(3);

        subscription.Expire(expiredAt);

        (subscription.Status, subscription.ExpiredAt).Should().Be((SubscriptionStatus.Expired, (DateTimeOffset?)expiredAt));
    }

    [Fact]
    public void Expire_AlreadyExpired_ThrowsAlreadyExpired()
    {
        var subscription = new SubscriptionBuilder().InStatus(SubscriptionStatus.Expired).Build();

        var act = () => subscription.Expire(MonthEnd.AddDays(5));

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.SubscriptionAlreadyExpired);
    }

    [Theory]
    [InlineData(SubscriptionStatus.Active, 3)]
    [InlineData(SubscriptionStatus.PastDue, 3)]
    [InlineData(SubscriptionStatus.Cancelled, 0)]
    [InlineData(SubscriptionStatus.Expired, null)]
    public void EntitledUntil_ByStatus_AddsGraceOnlyWhileBillable(SubscriptionStatus status, int? daysAfterEnd)
    {
        var subscription = new SubscriptionBuilder().InStatus(status).Build();

        var entitledUntil = subscription.EntitledUntil(Grace);

        entitledUntil.Should().Be(daysAfterEnd is null ? null : MonthEnd.AddDays(daysAfterEnd.Value));
    }

    [Fact]
    public void Lapse_ActiveBeforeEnd_ReturnsFalseAndKeepsActive()
    {
        var subscription = new SubscriptionBuilder().Build();

        var lapsed = subscription.Lapse(MonthEnd.AddTicks(-1), Grace);

        (lapsed, subscription.Status).Should().Be((false, SubscriptionStatus.Active));
    }

    [Fact]
    public void Lapse_ActiveAfterEndInsideGrace_BecomesPastDue()
    {
        var subscription = new SubscriptionBuilder().Build();

        var lapsed = subscription.Lapse(MonthEnd, Grace);

        (lapsed, subscription.Status, subscription.ExpiredAt).Should().Be((true, SubscriptionStatus.PastDue, (DateTimeOffset?)null));
    }

    [Fact]
    public void Lapse_PastDueAfterGrace_ExpiresAtGraceEnd()
    {
        var subscription = new SubscriptionBuilder().InStatus(SubscriptionStatus.PastDue).Build();

        var lapsed = subscription.Lapse(MonthEnd.AddDays(10), Grace);

        (lapsed, subscription.Status, subscription.ExpiredAt).Should().Be((true, SubscriptionStatus.Expired, (DateTimeOffset?)(MonthEnd + Grace)));
    }

    [Fact]
    public void Lapse_ActiveAfterGrace_ExpiresAtGraceEnd()
    {
        var subscription = new SubscriptionBuilder().Build();

        var lapsed = subscription.Lapse(MonthEnd + Grace, Grace);

        (lapsed, subscription.Status, subscription.ExpiredAt).Should().Be((true, SubscriptionStatus.Expired, (DateTimeOffset?)(MonthEnd + Grace)));
    }

    [Fact]
    public void Lapse_CancelledAfterEnd_ExpiresAtPeriodEnd()
    {
        var subscription = new SubscriptionBuilder().InStatus(SubscriptionStatus.Cancelled).Build();

        var lapsed = subscription.Lapse(MonthEnd.AddDays(1), Grace);

        (lapsed, subscription.Status, subscription.ExpiredAt).Should().Be((true, SubscriptionStatus.Expired, (DateTimeOffset?)MonthEnd));
    }

    [Fact]
    public void Lapse_Expired_ReturnsFalse()
    {
        var subscription = new SubscriptionBuilder().InStatus(SubscriptionStatus.Expired).Build();

        var lapsed = subscription.Lapse(MonthEnd.AddDays(30), Grace);

        (lapsed, subscription.Status, subscription.ExpiredAt).Should().Be((false, SubscriptionStatus.Expired, (DateTimeOffset?)MonthEnd));
    }
}
