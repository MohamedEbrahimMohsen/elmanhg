using Elmanhg.Application.Configuration.Shared;
using Elmanhg.Application.Shared.RuntimeSettings;
using Elmanhg.Domain.RuntimeSettings;
using MediatR;

namespace Elmanhg.Application.Configuration.GetRuntimeSettings;

public sealed class GetRuntimeSettingsHandler(IRuntimeSettingOverrideRepository runtimeSettingOverrideRepository, RuntimeSettingRegistry registry) : IRequestHandler<GetRuntimeSettingsQuery, List<RuntimeSettingGroupResult>>
{
    public async Task<List<RuntimeSettingGroupResult>> Handle(GetRuntimeSettingsQuery request, CancellationToken cancellationToken)
    {
        var rows = await runtimeSettingOverrideRepository.GetAllAsync(cancellationToken, asNoTracking: true).ConfigureAwait(false) ?? [];
        var values = RuntimeSettingValues.From(registry, rows);
        return registry.Definitions
            .GroupBy(x => x.Group)
            .Select(group => new RuntimeSettingGroupResult(group.Key, group.Select(definition => RuntimeSettingResultGenerator.Generate(definition, values, rows.FirstOrDefault(x => x.Key == definition.Key))).ToList()))
            .ToList();
    }
}
