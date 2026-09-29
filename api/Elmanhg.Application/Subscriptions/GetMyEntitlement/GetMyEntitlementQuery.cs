using Elmanhg.Application.Subscriptions.Shared;
using MediatR;

namespace Elmanhg.Application.Subscriptions.GetMyEntitlement;

public sealed record GetMyEntitlementQuery : IRequest<EntitlementResult>;
