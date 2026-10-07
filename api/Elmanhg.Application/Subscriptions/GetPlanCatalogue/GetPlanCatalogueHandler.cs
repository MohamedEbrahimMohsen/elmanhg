using Core.Settings;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.RuntimeSettings.Definitions;
using Elmanhg.Application.Subscriptions.Shared;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Subscriptions.GetPlanCatalogue;

public sealed class GetPlanCatalogueHandler(IOptions<SubscriptionsOptions> subscriptionsOptions, IRuntimeSettings runtimeSettings) : IRequestHandler<GetPlanCatalogueQuery, PlanCatalogueResult>
{
    public async Task<PlanCatalogueResult> Handle(GetPlanCatalogueQuery request, CancellationToken cancellationToken)
    {
        var values = await runtimeSettings.GetValuesAsync(cancellationToken).ConfigureAwait(false);
        return PlanCatalogueResultGenerator.Generate(subscriptionsOptions.Value, PlanLimits.From(values), values.Get(AskTeacherRuntimeSettings.ReplySlaHours));
    }
}
