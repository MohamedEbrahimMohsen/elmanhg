using Elmanhg.Application;
using Elmanhg.Application.Shared.Options;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.Exams;

public sealed class ExamsOptionsTests
{
    [Fact]
    public void AddApplication_DefaultExamsOptions_ResolvesWithoutThrowing()
    {
        using var provider = BuildProvider([]);

        var options = provider.GetRequiredService<IOptions<ExamsOptions>>().Value;

        (options.DeadlineGraceSeconds, options.AutoSubmitEnabled, options.AutoSubmitIntervalSeconds, options.AutoSubmitBatchSize, options.WeakestObjectiveCount).Should().Be((30, true, 60, 50, 3));
        options.DeadlineGrace.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void AddApplication_GraceOutOfRange_ThrowsOptionsValidationException()
    {
        using var provider = BuildProvider(new Dictionary<string, string?> { ["Exams:DeadlineGraceSeconds"] = "601" });

        var act = () => provider.GetRequiredService<IOptions<ExamsOptions>>().Value;

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
