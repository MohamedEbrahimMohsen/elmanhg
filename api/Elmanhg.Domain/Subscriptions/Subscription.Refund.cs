namespace Elmanhg.Domain.Subscriptions;

public partial class Subscription
{
    public bool RevokePaidPeriod(int periodMonths, DateTimeOffset revokedAt)
    {
        if (Status == SubscriptionStatus.Expired)
        {
            return false;
        }

        EnsurePeriod(periodMonths);
        var shortenedEnd = CurrentPeriodEnd.AddMonths(-periodMonths);
        if (shortenedEnd <= revokedAt)
        {
            CurrentPeriodEnd = revokedAt < CurrentPeriodEnd ? revokedAt : CurrentPeriodEnd;
            Status = SubscriptionStatus.Expired;
            ExpiredAt = revokedAt;
        }
        else
        {
            CurrentPeriodEnd = shortenedEnd;
        }

        CurrentPeriodStart = CurrentPeriodStart < CurrentPeriodEnd ? CurrentPeriodStart : CurrentPeriodEnd;
        return true;
    }
}
