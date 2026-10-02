using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.RuntimeSettings;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Shared.RuntimeSettings;

public sealed class CachedRuntimeSettings(IRuntimeSettingOverrideRepository runtimeSettingOverrideRepository, RuntimeSettingRegistry registry, IMemoryCache memoryCache, IOptions<RuntimeSettingsOptions> runtimeSettingsOptions) : IRuntimeSettings
{
    public async Task<T> GetAsync<T>(RuntimeSettingKey<T> key, CancellationToken cancellationToken)
    {
        var values = await GetValuesAsync(cancellationToken).ConfigureAwait(false);
        return values.Get(key);
    }

    public async Task<RuntimeSettingValues> GetValuesAsync(CancellationToken cancellationToken)
    {
        if (memoryCache.TryGetValue(RuntimeSettingsCache.Key, out RuntimeSettingValues? cached) && cached is not null)
        {
            return cached;
        }

        var rows = await runtimeSettingOverrideRepository.FindAsync(x => x.Value != null, cancellationToken, asNoTracking: true).ConfigureAwait(false);
        var values = RuntimeSettingValues.From(registry, rows);
        memoryCache.Set(RuntimeSettingsCache.Key, values, TimeSpan.FromSeconds(runtimeSettingsOptions.Value.CacheSeconds));
        return values;
    }
}
