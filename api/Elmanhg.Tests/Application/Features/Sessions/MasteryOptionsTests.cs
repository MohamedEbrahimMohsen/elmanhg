using Elmanhg.Application;
using Elmanhg.Application.Shared.Options;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.Sessions;

public sealed class MasteryOptionsTests
{
    [Fact]
    public void AddApplication_DefaultMasteryOptions_CorrectThresholdIsPointEight()
    {
        using var provider = BuildProvider([]);

        var options = provider.GetRequiredService<IOptions<MasteryOptions>>().Value;

        options.CorrectThreshold.Should().Be(0.8m);
    }

    [Fact]
    public void AddApplication_CorrectThresholdAboveOne_ThrowsOptionsValidationException()
    {
        using var provider = BuildProvider(new Dictionary<string, string?> { ["Mastery:CorrectThreshold"] = "1.5" });

        var act = () => provider.GetRequiredService<IOptions<MasteryOptions>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    private static ServiceProvider BuildProvider(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddApplication();
        return services.BuildServiceProvider();
    }
}
