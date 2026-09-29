using Elmanhg.Domain.SharedKernel;
using Elmanhg.Domain.Subscriptions;

namespace Elmanhg.Application.Payments.Shared;

public sealed record AdminPaymentResult(Guid Id, Guid StudentId, string StudentName, string? StudentContact, SubscriptionPlan Plan, BillingPeriod Period, int PeriodMonths, Money Amount, PaymentStatus Status, string? PaymobTransactionId, Guid? SubscriptionId, DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt, PaymentReviewReason? ReviewReason, bool NeedsReview, DateTimeOffset? ReviewResolvedAt, DateTimeOffset? RefundedAt, string? RefundReason, string? RefundTransactionId, bool CanRefund);
