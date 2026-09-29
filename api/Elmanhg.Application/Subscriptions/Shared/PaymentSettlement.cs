using Elmanhg.Domain.Subscriptions;

namespace Elmanhg.Application.Subscriptions.Shared;

public static class PaymentSettlement
{
    public static Subscription? Settle(Payment payment, bool succeeded, string transactionId, string rawNotification, int periodMonths, DateTimeOffset completedAt)
    {
        if (!succeeded)
        {
            payment.MarkFailed(transactionId, rawNotification, completedAt);
            return null;
        }

        var subscription = Subscription.Start(payment.StudentId, payment.Plan, payment.Period, periodMonths, completedAt, transactionId, payment.StudentId);
        payment.MarkSucceeded(subscription.Id, transactionId, rawNotification, completedAt);
        return subscription;
    }
}
