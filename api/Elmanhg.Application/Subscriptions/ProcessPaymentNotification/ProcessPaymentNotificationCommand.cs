using Core.Auditing;
using Elmanhg.Application.Subscriptions.Shared;
using MediatR;

namespace Elmanhg.Application.Subscriptions.ProcessPaymentNotification;

public sealed record ProcessPaymentNotificationCommand(string Payload, string? Signature) : IRequest<PaymentNotificationResult>, IAuditableCommand
{
    public string AuditAction => "Payment.ProcessNotification";
    public string AuditResourceType => "Payment";
    public Guid? AuditResourceId => null;
}
