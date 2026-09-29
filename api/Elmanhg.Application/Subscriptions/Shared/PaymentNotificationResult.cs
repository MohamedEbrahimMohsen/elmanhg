using Core.Auditing;

namespace Elmanhg.Application.Subscriptions.Shared;

public sealed record PaymentNotificationResult(Guid? PaymentId, PaymentNotificationOutcome Outcome) : IAuditableResult
{
    Guid? IAuditableResult.AuditResourceId => PaymentId;
}
