using Core.Auditing;
using Elmanhg.Application.Payments.Shared;
using MediatR;

namespace Elmanhg.Application.Payments.RefundPayment;

public sealed record RefundPaymentCommand(Guid PaymentId, string? Reason, Guid? IdempotencyKey) : IRequest<AdminPaymentResult>, IAuditableCommand
{
    public string AuditAction => "Payment.Refund";
    public string AuditResourceType => "Payment";
    public Guid? AuditResourceId => PaymentId;
}
