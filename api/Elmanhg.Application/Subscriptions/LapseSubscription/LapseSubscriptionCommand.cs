using Core.Auditing;
using MediatR;

namespace Elmanhg.Application.Subscriptions.LapseSubscription;

public sealed record LapseSubscriptionCommand(Guid SubscriptionId) : IRequest, IAuditableCommand
{
    public string AuditAction => "Subscription.Lapse";
    public string AuditResourceType => "Subscription";
    public Guid? AuditResourceId => SubscriptionId;
}
