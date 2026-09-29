using Core.Errors;
using Elmanhg.Domain.SharedKernel.Exceptions;

namespace Elmanhg.Domain.Subscriptions;

public partial class Subscription
{
    public void Renew(int periodMonths, string? paymobReference)
    {
        if (Status.HasEnded())
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.SubscriptionEnded);
        }

        EnsurePeriod(periodMonths);
        CurrentPeriodStart = CurrentPeriodEnd;
        CurrentPeriodEnd = CurrentPeriodEnd.AddMonths(periodMonths);
        Status = SubscriptionStatus.Active;
        PaymobReference = paymobReference ?? PaymobReference;
        UpdationDate = DateTimeOffset.UtcNow;
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
