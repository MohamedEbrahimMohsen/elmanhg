using Core.Observability;
using Elmanhg.Tests.Application.Features.Shared.Observability;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using System.Diagnostics.Metrics;

namespace Elmanhg.Tests.Core.Observability;

public sealed class RequestMetricsTests
{
    private readonly IMeterFactory _meterFactory = MeterFactories.Create();

    [Fact]
    public void RecordRequest_Success_RecordsCountAndDurationWithTags()
    {
        var metrics = new RequestMetrics(_meterFactory, "Probe.Meter", "probe");
        using var requests = new MetricCollector<long>(_meterFactory, "Probe.Meter", "probe.requests");
        using var duration = new MetricCollector<double>(_meterFactory, "Probe.Meter", "probe.request.duration");

        metrics.RecordRequest("X", RequestMetrics.SuccessOutcome, TimeSpan.FromMilliseconds(250));

        var request = requests.GetMeasurementSnapshot().Should().ContainSingle().Subject;
        request.Value.Should().Be(1);
        request.Tags.Should().Contain("probe.request", "X").And.Contain("probe.outcome", "Success");
        duration.GetMeasurementSnapshot().Should().ContainSingle().Which.Value.Should().Be(0.25);
    }

    [Fact]
    public void Constructor_Prefix_DerivesTagNames()
    {
        var metrics = new RequestMetrics(_meterFactory, "Probe.Meter", "probe");

        (metrics.RequestTag, metrics.OutcomeTag).Should().Be(("probe.request", "probe.outcome"));
    }
}
