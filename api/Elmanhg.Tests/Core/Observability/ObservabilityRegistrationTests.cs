using Core.Observability;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Elmanhg.Tests.Core.Observability;

public sealed class ObservabilityRegistrationTests
{
    private static readonly TelemetrySetup Setup = new("probe-api", ["Probe"], ["Probe"]);

    [Fact]
    public void AddCoreObservability_NoServiceNameConfigured_UsesSetupDefault()
    {
        using var provider = Provider();

        var options = provider.GetRequiredService<IOptions<ObservabilityOptions>>().Value;

        options.ServiceName.Should().Be("probe-api");
    }

    [Fact]
    public void AddCoreObservability_ServiceNameConfigured_OverridesSetupDefault()
    {
        using var provider = Provider(("Observability:ServiceName", "configured-api"));

        var options = provider.GetRequiredService<IOptions<ObservabilityOptions>>().Value;

        options.ServiceName.Should().Be("configured-api");
    }

    [Fact]
    public void AddCoreObservability_RelativeEndpoint_FailsValidation()
    {
        using var provider = Provider(("Observability:OtlpEndpoint", "collector/v1"));

        var act = () => provider.GetRequiredService<IOptions<ObservabilityOptions>>().Value;

        act.Should().Throw<OptionsValidationException>().Which.Message.Should().Contain("Observability:OtlpEndpoint");
    }

    private static ServiceProvider Provider(params (string Key, string Value)[] settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(x => new KeyValuePair<string, string?>(x.Key, x.Value)))
            .Build();
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns("Testing");
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddCoreObservability(configuration, environment, Setup);
        return services.BuildServiceProvider();
    }
}
