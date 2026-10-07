using Core.Settings;
using Elmanhg.Domain.RuntimeSettings;

namespace Elmanhg.Application.Shared.RuntimeSettings;

public sealed class RuntimeSettingOverrideStore(IRuntimeSettingOverrideRepository runtimeSettingOverrideRepository) : IRuntimeSettingOverrideStore
{
    public async Task<IReadOnlyList<IRuntimeSettingOverride>> GetOverridesAsync(CancellationToken cancellationToken)
    {
        return await runtimeSettingOverrideRepository.FindAsync(x => x.Value != null, cancellationToken, asNoTracking: true).ConfigureAwait(false);
    }
}
