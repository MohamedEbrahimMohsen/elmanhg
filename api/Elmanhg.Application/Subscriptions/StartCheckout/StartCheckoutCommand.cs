using Core.Auditing;
using Elmanhg.Application.Subscriptions.Shared;
using Elmanhg.Domain.Subscriptions;
using MediatR;
using System.Text.Json.Serialization;

namespace Elmanhg.Application.Subscriptions.StartCheckout;

public sealed record StartCheckoutCommand(SubscriptionPlan? Plan, BillingPeriod? Period) : IRequest<CheckoutResult>, IAuditableCommand
{
    [JsonIgnore]
    public string AuditAction => "Payment.StartCheckout";
    [JsonIgnore]
    public string AuditResourceType => "Payment";
    [JsonIgnore]
    public Guid? AuditResourceId => null;
}
