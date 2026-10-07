using Core.Errors;
using Elmanhg.Domain.SharedKernel.Exceptions;

namespace Elmanhg.Domain.Subscriptions;

public partial class Payment
{
    public DateTimeOffset? RefundedAt { get; private set; }
    public Guid? RefundedBy { get; private set; }
    public string? RefundReason { get; private set; }
    public string? RefundTransactionId { get; private set; }
    public Guid? RefundIdempotencyKey { get; private set; }
    public DateTimeOffset? ReviewResolvedAt { get; private set; }
    public Guid? ReviewResolvedBy { get; private set; }

    public bool NeedsReview => ReviewReason is not null && ReviewResolvedAt is null;

    public bool IsRefundable => Status == PaymentStatus.Succeeded && PaymobTransactionId is not null;

    public bool IsRefundReplay(Guid? idempotencyKey) => Status == PaymentStatus.Refunded && idempotencyKey is not null && RefundIdempotencyKey == idempotencyKey;

    public void EnsureRefundable()
    {
        if (Status == PaymentStatus.Refunded)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.PaymentAlreadyRefunded);
        }

        if (!IsRefundable)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.PaymentNotRefundable);
        }
    }

    public void MarkRefunded(string refundTransactionId, DateTimeOffset refundedAt, Guid? refundedBy, string? reason, Guid? idempotencyKey)
    {
        EnsureRefundable();
        if (NeedsReview)
        {
            ReviewResolvedAt = refundedAt;
            ReviewResolvedBy = refundedBy;
        }

        Status = PaymentStatus.Refunded;
        RefundTransactionId = refundTransactionId;
        RefundedAt = refundedAt;
        RefundedBy = refundedBy;
        RefundReason = reason;
        RefundIdempotencyKey = idempotencyKey;
    }

    public void ResolveReview(Guid resolvedBy, DateTimeOffset resolvedAt)
    {
        if (!NeedsReview)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.PaymentReviewNotOpen);
        }

        ReviewResolvedAt = resolvedAt;
        ReviewResolvedBy = resolvedBy;
    }
}
