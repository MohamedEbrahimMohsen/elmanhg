using Elmanhg.Domain.Subscriptions;

namespace Elmanhg.Tests.Builders;

public sealed class SubscriptionBuilder
{
    public const string Reference = "paymob-ref-1";

    public static readonly DateTimeOffset DefaultStart = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private Guid _studentId = Guid.NewGuid();
    private SubscriptionPlan _plan = SubscriptionPlan.Base;
    private BillingPeriod _period = BillingPeriod.Monthly;
    private int _months = 1;
    private DateTimeOffset _start = DefaultStart;
    private SubscriptionStatus _status = SubscriptionStatus.Active;

    public SubscriptionBuilder ForStudent(Guid studentId)
    {
        _studentId = studentId;
        return this;
    }

    public SubscriptionBuilder WithPlan(SubscriptionPlan plan)
    {
        _plan = plan;
        return this;
    }

    public SubscriptionBuilder WithPeriod(BillingPeriod period, int months)
    {
        _period = period;
        _months = months;
        return this;
    }

    public SubscriptionBuilder StartingAt(DateTimeOffset start)
    {
        _start = start;
        return this;
    }

    public SubscriptionBuilder InStatus(SubscriptionStatus status)
    {
        _status = status;
        return this;
    }

    public Subscription Build()
    {
        var subscription = Subscription.Start(_studentId, _plan, _period, _months, _start, Reference, _studentId);
        Action transition = _status switch
        {
            SubscriptionStatus.PastDue => subscription.MarkPastDue,
            SubscriptionStatus.Cancelled => () => subscription.Cancel(_start.AddDays(1)),
            SubscriptionStatus.Expired => () => subscription.Expire(subscription.CurrentPeriodEnd),
            _ => () => { },
        };
        transition();
        return subscription;
    }
}
