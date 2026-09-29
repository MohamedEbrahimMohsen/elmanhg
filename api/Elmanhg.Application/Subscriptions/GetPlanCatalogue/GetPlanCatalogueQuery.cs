using Elmanhg.Application.Subscriptions.Shared;
using MediatR;

namespace Elmanhg.Application.Subscriptions.GetPlanCatalogue;

public sealed record GetPlanCatalogueQuery : IRequest<PlanCatalogueResult>;
