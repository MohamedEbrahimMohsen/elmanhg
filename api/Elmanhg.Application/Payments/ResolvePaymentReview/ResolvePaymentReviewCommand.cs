using Core.Auditing;
using Elmanhg.Application.Payments.Shared;
using MediatR;

namespace Elmanhg.Application.Payments.ResolvePaymentReview;

public sealed record ResolvePaymentReviewCommand(Guid PaymentId) : IRequest<AdminPaymentResult>, IAuditableCommand
{
    public string AuditAction => "Payment.ResolveReview";
    public string AuditResourceType => "Payment";
    public Guid? AuditResourceId => PaymentId;
}
