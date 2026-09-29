using Core.Auditing;
using Elmanhg.Application.Subscriptions.Shared;
using MediatR;

namespace Elmanhg.Application.Subscriptions.CompleteFakePayment;

public sealed record CompleteFakePaymentCommand(Guid PaymentId, bool Succeeded) : IRequest<PaymentResult>, IAuditableCommand
{
    public string AuditAction => "Payment.CompleteFake";
    public string AuditResourceType => "Payment";
    public Guid? AuditResourceId => PaymentId;
}
