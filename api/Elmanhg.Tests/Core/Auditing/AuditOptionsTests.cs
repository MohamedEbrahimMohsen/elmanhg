using Core.Auditing;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Elmanhg.Tests.Core.Auditing;

public sealed class AuditOptionsTests
{
    [Fact]
    public void Enabled_NotConfigured_DefaultsToTrue()
    {
        var options = new AuditOptions();

        options.Enabled.Should().BeTrue();
    }

    [Fact]
    public void AddCoreAuditing_EmptyConfiguration_RegistersAuditBehaviour()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddCoreAuditing(configuration);

        services.Should().Contain(d => d.ServiceType == typeof(IPipelineBehavior<,>) && d.ImplementationType == typeof(AuditBehaviour<,>));
    }

    [Fact]
    public void AddCoreAuditing_ExplicitlyDisabled_DoesNotRegisterAuditBehaviour()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["CoreAuditing:Enabled"] = "false" }).Build();

        services.AddCoreAuditing(configuration);

        services.Should().NotContain(d => d.ImplementationType == typeof(AuditBehaviour<,>));
    }
}
