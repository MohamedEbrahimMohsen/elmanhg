using Core.Auditing;
using Elmanhg.Application.Subscriptions.Shared;
using MediatR;

namespace Elmanhg.Application.Subscriptions.CancelSubscription;

public sealed record CancelSubscriptionCommand(Guid SubscriptionId) : IRequest<EntitlementResult>, IAuditableCommand
{
    public string AuditAction => "Subscription.Cancel";
    public string AuditResourceType => "Subscription";
    public Guid? AuditResourceId => SubscriptionId;
}
