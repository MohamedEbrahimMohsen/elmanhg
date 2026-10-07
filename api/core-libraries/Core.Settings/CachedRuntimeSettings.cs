using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Core.Settings;

public sealed class CachedRuntimeSettings(IRuntimeSettingOverrideStore runtimeSettingOverrideStore, RuntimeSettingRegistry registry, IMemoryCache memoryCache, IOptions<RuntimeSettingsOptions> runtimeSettingsOptions) : IRuntimeSettings
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

        var overrides = await runtimeSettingOverrideStore.GetOverridesAsync(cancellationToken).ConfigureAwait(false);
        var values = RuntimeSettingValues.From(registry, overrides);
        memoryCache.Set(RuntimeSettingsCache.Key, values, TimeSpan.FromSeconds(runtimeSettingsOptions.Value.CacheSeconds));
        return values;
    }
}
