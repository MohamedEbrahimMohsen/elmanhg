using Core.DDD.Entities;
using Core.Errors;
using Elmanhg.Domain.SharedKernel.Exceptions;

namespace Elmanhg.Domain.Subscriptions;

public partial class Subscription : AuditEntity, IAuditedEntity
{
    public Guid StudentId { get; private set; }
    public SubscriptionPlan Plan { get; private set; }
    public BillingPeriod Period { get; private set; }
    public SubscriptionStatus Status { get; private set; }
    public DateTimeOffset CurrentPeriodStart { get; private set; }
    public DateTimeOffset CurrentPeriodEnd { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public DateTimeOffset? ExpiredAt { get; private set; }
    public string? PaymobReference { get; private set; }
    public uint Version { get; private set; }

    private Subscription(Guid id, Guid? createdBy) : base(id, createdBy) { }

    public static Subscription Start(Guid studentId, SubscriptionPlan plan, BillingPeriod period, int periodMonths, DateTimeOffset startsAt, string? paymobReference, Guid? createdBy)
    {
        EnsurePeriod(periodMonths);
        return new Subscription(Guid.NewGuid(), createdBy)
        {
            StudentId = studentId,
            Plan = plan,
            Period = period,
            Status = SubscriptionStatus.Active,
            CurrentPeriodStart = startsAt,
            CurrentPeriodEnd = startsAt.AddMonths(periodMonths),
            PaymobReference = paymobReference,
        };
    }

    public DateTimeOffset? EntitledUntil(TimeSpan gracePeriod)
    {
        return Status switch
        {
            SubscriptionStatus.Active or SubscriptionStatus.PastDue => CurrentPeriodEnd + gracePeriod,
            SubscriptionStatus.Cancelled => CurrentPeriodEnd,
            _ => null,
        };
    }

    public bool IsEntitledAt(DateTimeOffset now, TimeSpan gracePeriod) => EntitledUntil(gracePeriod) > now;

    public bool IsRenewableAt(DateTimeOffset now, TimeSpan renewalWindow) => Status != SubscriptionStatus.Expired && CurrentPeriodEnd - renewalWindow <= now;

    private static void EnsurePeriod(int periodMonths)
    {
        if (periodMonths < 1)
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.SubscriptionPeriodInvalid);
        }
    }
}
