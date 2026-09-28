using Elmanhg.Application;
using Elmanhg.Application.Shared.Options;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.Sessions;

public sealed class SessionsOptionsTests
{
    [Fact]
    public void AddApplication_MinQuizSizeAboveMax_ThrowsOptionsValidationException()
    {
        using var provider = BuildProvider(new Dictionary<string, string?> { ["Sessions:MinQuizSize"] = "15", ["Sessions:MaxQuizSize"] = "10" });

        var act = () => provider.GetRequiredService<IOptions<SessionsOptions>>().Value;

        act.Should().Throw<OptionsValidationException>().Which.Failures.Should().Contain("Sessions:MinQuizSize <= DefaultQuizSize <= MaxQuizSize is required.");
    }

    [Fact]
    public void AddApplication_DefaultQuizSizeOutsideRange_ThrowsOptionsValidationException()
    {
        using var provider = BuildProvider(new Dictionary<string, string?> { ["Sessions:DefaultQuizSize"] = "30" });

        var act = () => provider.GetRequiredService<IOptions<SessionsOptions>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void AddApplication_DefaultSessionsOptions_ResolvesWithoutThrowing()
    {
        using var provider = BuildProvider([]);

        var options = provider.GetRequiredService<IOptions<SessionsOptions>>().Value;

        (options.MinQuizSize, options.DefaultQuizSize, options.MaxQuizSize).Should().Be((5, 10, 20));
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
