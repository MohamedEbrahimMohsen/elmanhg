using Core.Http;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Core.Http;

public sealed class CoreHttpDependencyInjectionTests
{
    [Fact]
    public void AddCoreHttp_NoConfiguration_UsesDefaultUserAgent()
    {
        using var provider = BuildProvider([]);

        provider.GetRequiredService<IOptions<CoreHttpOptions>>().Value.UserAgent.Should().Be("Elmanhg/1.0");
    }

    [Fact]
    public void AddCoreHttp_ConfiguredUserAgent_OverridesDefault()
    {
        using var provider = BuildProvider(new() { ["CoreHttp:UserAgent"] = "Probe/2.0" });

        provider.GetRequiredService<IOptions<CoreHttpOptions>>().Value.UserAgent.Should().Be("Probe/2.0");
    }

    private static ServiceProvider BuildProvider(Dictionary<string, string?> configuration)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(configuration).Build());
        services.AddCoreHttp(CoreHttpTestSettings.UserAgent);
        return services.BuildServiceProvider();
    }
}
