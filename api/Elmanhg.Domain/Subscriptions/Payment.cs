using Core.DDD.Entities;
using Core.Errors;
using Elmanhg.Domain.SharedKernel;
using Elmanhg.Domain.SharedKernel.Exceptions;

namespace Elmanhg.Domain.Subscriptions;

public partial class Payment : AuditEntity, IAuditedEntity, IVersioned
{
    public Guid StudentId { get; private set; }
    public Guid? SubscriptionId { get; private set; }
    public SubscriptionPlan Plan { get; private set; }
    public BillingPeriod Period { get; private set; }
    public long AmountMinor { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public PaymentStatus Status { get; private set; }
    public string? PaymobTransactionId { get; private set; }
    public string? RawWebhook { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public int PeriodMonths { get; private set; }
    public string? ProviderOrderId { get; private set; }
    public PaymentReviewReason? ReviewReason { get; private set; }
    public uint Version { get; private set; }

    public Money Amount => new(AmountMinor, Currency);

    private Payment(Guid id, Guid? createdBy) : base(id, createdBy) { }

    public static Payment Create(Guid studentId, SubscriptionPlan plan, BillingPeriod period, int periodMonths, Money amount)
    {
        if (amount.AmountMinor <= 0 || amount.Currency.Length != 3 || !amount.Currency.All(char.IsAsciiLetterUpper))
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.PaymentAmountInvalid);
        }

        if (periodMonths < 1)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.SubscriptionPeriodInvalid);
        }

        return new Payment(Guid.NewGuid(), studentId)
        {
            StudentId = studentId,
            Plan = plan,
            Period = period,
            PeriodMonths = periodMonths,
            AmountMinor = amount.AmountMinor,
            Currency = amount.Currency,
            Status = PaymentStatus.Pending,
        };
    }

    public void MarkSucceeded(Guid subscriptionId, string paymobTransactionId, string rawWebhook, DateTimeOffset completedAt)
    {
        if (Status is PaymentStatus.Succeeded or PaymentStatus.Refunded)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.PaymentNotPending);
        }

        SubscriptionId = subscriptionId;
        PaymobTransactionId = paymobTransactionId;
        RawWebhook = rawWebhook;
        CompletedAt = completedAt;
        Status = PaymentStatus.Succeeded;
    }

    public void MarkFailed(string paymobTransactionId, string rawWebhook, DateTimeOffset completedAt)
    {
        EnsurePending();
        PaymobTransactionId = paymobTransactionId;
        RawWebhook = rawWebhook;
        CompletedAt = completedAt;
        Status = PaymentStatus.Failed;
    }

    public void LinkProviderOrder(string providerOrderId)
    {
        EnsurePending();
        ProviderOrderId = providerOrderId;
    }

    public void FlagForReview(PaymentReviewReason reason)
    {
        ReviewReason = reason;
        ReviewResolvedAt = null;
        ReviewResolvedBy = null;
        UpdationDate = DateTimeOffset.UtcNow;
    }

    private void EnsurePending()
    {
        if (Status != PaymentStatus.Pending)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.PaymentNotPending);
        }
    }
}
