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
    private static readonly TimeSpan Grace = TimeSpan.FromDays(3);

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

        subscription.Renew(4, "ref-2");

        (subscription.CurrentPeriodStart, subscription.CurrentPeriodEnd, subscription.Status).Should().Be((MonthEnd, MonthEnd.AddMonths(4), SubscriptionStatus.Active));
    }

    [Fact]
    public void Renew_PastDue_ReactivatesAndExtends()
    {
        var subscription = new SubscriptionBuilder().InStatus(SubscriptionStatus.PastDue).Build();

        subscription.Renew(1, null);

        (subscription.Status, subscription.CurrentPeriodEnd).Should().Be((SubscriptionStatus.Active, MonthEnd.AddMonths(1)));
    }

    [Theory]
    [InlineData(SubscriptionStatus.Cancelled)]
    [InlineData(SubscriptionStatus.Expired)]
    public void Renew_CancelledOrExpired_ThrowsSubscriptionEnded(SubscriptionStatus status)
    {
        var subscription = new SubscriptionBuilder().InStatus(status).Build();

        var act = () => subscription.Renew(1, null);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.SubscriptionEnded);
        (subscription.CurrentPeriodStart, subscription.CurrentPeriodEnd).Should().Be((Start, MonthEnd));
    }

    [Fact]
    public void Renew_PeriodMonthsBelowOne_ThrowsPeriodInvalid()
    {
        var subscription = new SubscriptionBuilder().Build();

        var act = () => subscription.Renew(0, null);

        act.Should().Throw<BusinessRuleViolationCoreException>().Which.ErrorCode.Should().Be(ErrorCodes.SubscriptionPeriodInvalid);
        subscription.CurrentPeriodEnd.Should().Be(MonthEnd);
    }

    [Fact]
    public void Renew_WithoutReference_KeepsPreviousReference()
    {
        var subscription = new SubscriptionBuilder().Build();

        subscription.Renew(1, null);

        subscription.PaymobReference.Should().Be(SubscriptionBuilder.Reference);
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
}
