using Core.Http;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Elmanhg.Tests.Core.Http;

public sealed class ProviderSwitchServiceCollectionExtensionsTests
{
    [Fact]
    public void AddProviderSwitch_UseRealTrue_ResolvesReal()
    {
        using var provider = BuildProvider(useReal: true);
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IProbeService>().Should().BeOfType<RealProbeService>();
    }

    [Fact]
    public void AddProviderSwitch_UseRealFalse_ResolvesFake()
    {
        using var provider = BuildProvider(useReal: false);
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IProbeService>().Should().BeOfType<FakeProbeService>();
    }

    [Fact]
    public void AddProviderSwitch_FakeFactoryAndUseRealFalse_ResolvesFactoryInstance()
    {
        var fake = new FakeProbeService();
        var services = new ServiceCollection();
        services.AddScoped<RealProbeService>();
        services.AddProviderSwitch<IProbeService, RealProbeService>(_ => false, _ => fake);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var resolved = scope.ServiceProvider.GetRequiredService<IProbeService>();

        resolved.Should().BeSameAs(fake);
    }

    private static ServiceProvider BuildProvider(bool useReal)
    {
        var services = new ServiceCollection();
        services.AddScoped<FakeProbeService>();
        services.AddScoped<RealProbeService>();
        services.AddProviderSwitch<IProbeService, FakeProbeService, RealProbeService>(_ => useReal);
        return services.BuildServiceProvider();
    }

    public interface IProbeService;

    public sealed class FakeProbeService : IProbeService;

    public sealed class RealProbeService : IProbeService;
}
