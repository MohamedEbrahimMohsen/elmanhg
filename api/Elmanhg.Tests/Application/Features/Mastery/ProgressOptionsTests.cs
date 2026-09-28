using Elmanhg.Application;
using Elmanhg.Application.Shared.Options;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.Mastery;

public sealed class ProgressOptionsTests
{
    [Fact]
    public void AddApplication_DefaultProgressOptions_UseCairoAndOneYear()
    {
        using var provider = BuildProvider([]);

        var options = provider.GetRequiredService<IOptions<ProgressOptions>>().Value;

        (options.StreakTimeZone, options.StreakMaxDays).Should().Be(("Africa/Cairo", 365));
    }

    [Fact]
    public void AddApplication_UnknownTimeZone_ThrowsOptionsValidationException()
    {
        using var provider = BuildProvider(new Dictionary<string, string?> { ["Progress:StreakTimeZone"] = "Mars/Olympus" });

        var act = () => provider.GetRequiredService<IOptions<ProgressOptions>>().Value;

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
