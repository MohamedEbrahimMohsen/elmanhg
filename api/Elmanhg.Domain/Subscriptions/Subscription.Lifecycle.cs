using Core.Errors;
using Elmanhg.Domain.SharedKernel.Exceptions;

namespace Elmanhg.Domain.Subscriptions;

public partial class Subscription
{
    public void Renew(BillingPeriod period, int periodMonths, string? paymobReference, DateTimeOffset renewedAt, TimeSpan gracePeriod)
    {
        if (!IsEntitledAt(renewedAt, gracePeriod))
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.SubscriptionEnded);
        }

        EnsurePeriod(periodMonths);
        Period = period;
        CurrentPeriodStart = CurrentPeriodEnd;
        CurrentPeriodEnd = CurrentPeriodEnd.AddMonths(periodMonths);
        Status = SubscriptionStatus.Active;
        CancelledAt = null;
        PaymobReference = paymobReference ?? PaymobReference;
        UpdationDate = DateTimeOffset.UtcNow;
    }

    public bool Lapse(DateTimeOffset now, TimeSpan gracePeriod)
    {
        if (EntitledUntil(gracePeriod) is { } lapsedAt && lapsedAt <= now)
        {
            Expire(lapsedAt);
            return true;
        }

        if (Status == SubscriptionStatus.Active && CurrentPeriodEnd <= now)
        {
            MarkPastDue();
            return true;
        }

        return false;
    }

    public void MarkPastDue()
    {
        if (Status != SubscriptionStatus.Active)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.SubscriptionNotActive);
        }

        Status = SubscriptionStatus.PastDue;
        UpdationDate = DateTimeOffset.UtcNow;
    }

    public void Cancel(DateTimeOffset cancelledAt)
    {
        if (Status.HasEnded())
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.SubscriptionEnded);
        }

        Status = SubscriptionStatus.Cancelled;
        CancelledAt = cancelledAt;
        UpdationDate = DateTimeOffset.UtcNow;
    }

    public void Expire(DateTimeOffset expiredAt)
    {
        if (Status == SubscriptionStatus.Expired)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.SubscriptionAlreadyExpired);
        }

        Status = SubscriptionStatus.Expired;
        ExpiredAt = expiredAt;
        UpdationDate = DateTimeOffset.UtcNow;
    }
}
