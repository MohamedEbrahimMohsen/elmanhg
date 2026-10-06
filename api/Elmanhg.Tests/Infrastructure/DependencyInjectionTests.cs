using Core.Http;
using Elmanhg.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Elmanhg.Tests.Infrastructure;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddInfrastructure_NoUserAgentConfigured_SendsElmanhgProductToken()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IHostEnvironment>());
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddInfrastructure();
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<CoreHttpOptions>>().Value.UserAgent.Should().Be("Elmanhg/1.0");
    }
}
