using Core.Settings;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Core.Settings;

public sealed class CoreSettingsDependencyInjectionTests
{
    [Fact]
    public void AddCoreRuntimeSettings_Registered_ResolvesCachedReaderAndOrderedRegistry()
    {
        using var provider = Provider(new ProbeRuntimeSettings());
        using var scope = provider.CreateScope();

        var settings = scope.ServiceProvider.GetRequiredService<IRuntimeSettings>();
        var store = scope.ServiceProvider.GetRequiredService<IRuntimeSettingOverrideStore>();
        var registry = scope.ServiceProvider.GetRequiredService<RuntimeSettingRegistry>();

        settings.Should().BeOfType<CachedRuntimeSettings>();
        store.Should().BeOfType<ProbeOverrideStore>();
        registry.Definitions[0].Key.Should().Be("probe.count");
    }

    [Fact]
    public void AddCoreRuntimeSettings_CacheSecondsOutOfRange_FailsValidation()
    {
        using var provider = Provider(new ProbeRuntimeSettings(), ("RuntimeSettings:CacheSeconds", "0"));

        var act = () => provider.GetRequiredService<IOptions<RuntimeSettingsOptions>>().Value;

        act.Should().Throw<OptionsValidationException>().Which.Message.Should().Contain("CacheSeconds");
    }

    [Fact]
    public void AddCoreRuntimeSettings_DefinitionGroupNotInOrder_FailsValidation()
    {
        using var provider = Provider(new ProbeRuntimeSettings(countGroup: "Other"));

        var act = () => provider.GetRequiredService<IOptions<RuntimeSettingsOptions>>().Value;

        act.Should().Throw<OptionsValidationException>().Which.Message.Should().Contain("group Other");
    }

    private static ServiceProvider Provider(ProbeRuntimeSettings definitions, params (string Key, string Value)[] settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(x => new KeyValuePair<string, string?>(x.Key, x.Value)))
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IRuntimeSettingDefinitions>(definitions);
        services.AddCoreRuntimeSettings<ProbeOverrideStore>(ProbeRuntimeSettings.GroupOrder);
        return services.BuildServiceProvider();
    }
}
