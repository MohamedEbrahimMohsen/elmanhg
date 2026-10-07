using Core.Observability;
using Core.Queues;
using Elmanhg.Api.Hosting;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Diagnostics.Metrics;

namespace Elmanhg.Tests.Api.Hosting;

public sealed class ObservabilityExtensionsTests
{
    [Fact]
    public void AddElmanhgObservability_RecordedRequest_UsesElmanhgMeterAndMetricNames()
    {
        using var provider = Provider();
        var metrics = provider.GetRequiredService<RequestMetrics>();
        using var requests = new MetricCollector<long>(provider.GetRequiredService<IMeterFactory>(), "Elmanhg", "elmanhg.requests");

        metrics.RecordRequest("X", "Success", TimeSpan.FromMilliseconds(250));

        var request = requests.GetMeasurementSnapshot().Should().ContainSingle().Subject;
        request.Value.Should().Be(1);
        request.Tags.Should().Contain("elmanhg.request", "X").And.Contain("elmanhg.outcome", "Success");
    }

    [Fact]
    public void AddElmanhgObservability_JobRun_UsesElmanhgJobMetricNames()
    {
        using var provider = Provider();
        var metrics = provider.GetRequiredService<BackgroundJobMetrics>();
        using var runs = new MetricCollector<long>(provider.GetRequiredService<IMeterFactory>(), "Elmanhg", "elmanhg.job.runs");

        metrics.StartRun("x").Dispose();

        var run = runs.GetMeasurementSnapshot().Should().ContainSingle().Subject;
        run.Value.Should().Be(1);
        run.Tags.Should().Contain("elmanhg.job", "x").And.Contain("elmanhg.outcome", "Succeeded");
    }

    [Fact]
    public void AddElmanhgObservability_NoServiceNameConfigured_UsesElmanhgApi()
    {
        using var provider = Provider();

        var options = provider.GetRequiredService<IOptions<ObservabilityOptions>>().Value;

        options.ServiceName.Should().Be("elmanhg-api");
    }

    private static ServiceProvider Provider()
    {
        var configuration = new ConfigurationBuilder().Build();
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns("Testing");
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddMetrics();
        services.AddElmanhgObservability(configuration, environment);
        return services.BuildServiceProvider();
    }
}
