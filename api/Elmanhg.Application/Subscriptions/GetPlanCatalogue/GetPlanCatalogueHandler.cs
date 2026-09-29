using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Subscriptions.Shared;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Subscriptions.GetPlanCatalogue;

public sealed class GetPlanCatalogueHandler(IOptions<SubscriptionsOptions> subscriptionsOptions) : IRequestHandler<GetPlanCatalogueQuery, PlanCatalogueResult>
{
    public Task<PlanCatalogueResult> Handle(GetPlanCatalogueQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(PlanCatalogueResultGenerator.Generate(subscriptionsOptions.Value));
    }
}
