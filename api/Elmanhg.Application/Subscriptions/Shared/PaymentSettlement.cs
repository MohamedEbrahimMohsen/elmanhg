using Elmanhg.Domain.Subscriptions;

namespace Elmanhg.Application.Subscriptions.Shared;

public static class PaymentSettlement
{
    public static Subscription? Succeed(Payment payment, StudentEntitlement entitlement, string transactionId, string rawNotification, TimeSpan gracePeriod, DateTimeOffset completedAt)
    {
        var held = entitlement.Held(payment.Plan);
        Subscription? started = null;
        if (held is not null)
        {
            payment.MarkSucceeded(held.Id, transactionId, rawNotification, completedAt);
            held.Renew(payment.Period, payment.PeriodMonths, transactionId, completedAt, gracePeriod);
        }
        else
        {
            started = Subscription.Start(payment.StudentId, payment.Plan, payment.Period, payment.PeriodMonths, completedAt, transactionId, payment.StudentId);
            payment.MarkSucceeded(started.Id, transactionId, rawNotification, completedAt);
        }

        if (payment.Plan == SubscriptionPlan.AskTeacher && entitlement.BaseSubscription is null)
        {
            payment.FlagForReview(PaymentReviewReason.AskTeacherWithoutBase);
        }

        return started;
    }

    public static void Fail(Payment payment, string transactionId, string rawNotification, DateTimeOffset completedAt) => payment.MarkFailed(transactionId, rawNotification, completedAt);
}
