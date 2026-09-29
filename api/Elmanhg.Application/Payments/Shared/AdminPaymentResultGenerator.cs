using Elmanhg.Domain.Identity;
using Elmanhg.Domain.Subscriptions;

namespace Elmanhg.Application.Payments.Shared;

public static class AdminPaymentResultGenerator
{
    public static AdminPaymentResult Generate(Payment payment, User? student) => new(payment.Id, payment.StudentId, student?.DisplayName ?? string.Empty, student?.Email ?? student?.PhoneNumber, payment.Plan, payment.Period, payment.PeriodMonths, payment.Amount, payment.Status, payment.PaymobTransactionId, payment.SubscriptionId, payment.CreationDate, payment.CompletedAt, payment.ReviewReason, payment.NeedsReview, payment.ReviewResolvedAt, payment.RefundedAt, payment.RefundReason, payment.RefundTransactionId, payment.IsRefundable);
}
