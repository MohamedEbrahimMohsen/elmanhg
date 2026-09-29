using Elmanhg.Domain.Subscriptions;

namespace Elmanhg.Application.Subscriptions.Shared;

public static class PaymentRefundSettlement
{
    public static void Apply(Payment payment, Subscription? subscription, string refundTransactionId, DateTimeOffset refundedAt, Guid? refundedBy, string? reason, Guid? idempotencyKey)
    {
        payment.MarkRefunded(refundTransactionId, refundedAt, refundedBy, reason, idempotencyKey);
        subscription?.RevokePaidPeriod(payment.PeriodMonths, refundedAt);
    }
}
