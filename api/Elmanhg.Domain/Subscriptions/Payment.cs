using Core.DDD.Entities;
using Core.Errors;
using Elmanhg.Domain.SharedKernel;
using Elmanhg.Domain.SharedKernel.Exceptions;

namespace Elmanhg.Domain.Subscriptions;

public class Payment : AuditEntity, IAuditedEntity
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

    public Money Amount => new(AmountMinor, Currency);

    private Payment(Guid id, Guid? createdBy) : base(id, createdBy) { }

    public static Payment Create(Guid studentId, SubscriptionPlan plan, BillingPeriod period, Money amount)
    {
        if (amount.AmountMinor <= 0 || amount.Currency.Length != 3 || !amount.Currency.All(char.IsAsciiLetterUpper))
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.PaymentAmountInvalid);
        }

        return new Payment(Guid.NewGuid(), studentId)
        {
            StudentId = studentId,
            Plan = plan,
            Period = period,
            AmountMinor = amount.AmountMinor,
            Currency = amount.Currency,
            Status = PaymentStatus.Pending,
        };
    }

    public void MarkSucceeded(Guid subscriptionId, string paymobTransactionId, string rawWebhook, DateTimeOffset completedAt)
    {
        EnsurePending();
        SubscriptionId = subscriptionId;
        PaymobTransactionId = paymobTransactionId;
        RawWebhook = rawWebhook;
        CompletedAt = completedAt;
        Status = PaymentStatus.Succeeded;
        UpdationDate = DateTimeOffset.UtcNow;
    }

    public void MarkFailed(string paymobTransactionId, string rawWebhook, DateTimeOffset completedAt)
    {
        EnsurePending();
        PaymobTransactionId = paymobTransactionId;
        RawWebhook = rawWebhook;
        CompletedAt = completedAt;
        Status = PaymentStatus.Failed;
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
