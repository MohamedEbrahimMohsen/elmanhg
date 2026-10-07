using Core.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Core.Settings;

public static class DependencyInjection
{
    public static IServiceCollection AddCoreRuntimeSettings<TOverrideStore>(this IServiceCollection services, IReadOnlyList<string> groupOrder) where TOverrideStore : class, IRuntimeSettingOverrideStore
    {
        services.AddMemoryCache();
        services.AddValidatedOptions<RuntimeSettingsOptions>(RuntimeSettingsOptions.SectionName);
        services.AddSingleton<IValidateOptions<RuntimeSettingsOptions>>(serviceProvider => new RuntimeSettingsOptionsValidator(serviceProvider.GetServices<IRuntimeSettingDefinitions>(), groupOrder));
        services.AddSingleton(serviceProvider => new RuntimeSettingRegistry(serviceProvider.GetServices<IRuntimeSettingDefinitions>(), groupOrder));
        services.AddScoped<IRuntimeSettingOverrideStore, TOverrideStore>();
        services.AddScoped<IRuntimeSettings, CachedRuntimeSettings>();
        return services;
    }
}
