namespace Elmanhg.Domain.Subscriptions;

public enum SubscriptionStatus { Active, PastDue, Cancelled, Expired }

public static class SubscriptionStatusExtensions
{
    public static bool HasEnded(this SubscriptionStatus status) => status is SubscriptionStatus.Cancelled or SubscriptionStatus.Expired;
}
