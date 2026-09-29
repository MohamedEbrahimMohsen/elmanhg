using Core.Auditing;
using Elmanhg.Domain.SharedKernel;

namespace Elmanhg.Application.Subscriptions.Shared;

public sealed record CheckoutResult(Guid PaymentId, string RedirectUrl, Money Amount) : IAuditableResult
{
    Guid? IAuditableResult.AuditResourceId => PaymentId;
}
