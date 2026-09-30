using Core.Auditing;
using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Domain.Subscriptions;
using MediatR;

namespace Elmanhg.Application.Subscriptions.GrantComplimentarySubscription;

public sealed record GrantComplimentarySubscriptionCommand(Guid StudentId, SubscriptionPlan Plan, BillingPeriod Period) : IRequest<AdminSubscriptionResult>, IAuditableCommand
{
    public string AuditAction => "Subscription.GrantComplimentary";
    public string AuditResourceType => "Subscription";
    public Guid? AuditResourceId => null;
}
