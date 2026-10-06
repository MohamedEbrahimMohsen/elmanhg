using Core.CQRS.Behaviours;
using Core.Errors;
using Core.Observability;
using Elmanhg.Tests.Application.Features.Shared.Observability;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using System.Diagnostics.Metrics;

namespace Elmanhg.Tests.Core.Observability;

public sealed record MetricsProbeRequest : IRequest<Unit>;

public sealed class RequestMetricsBehaviourTests : IDisposable
{
    private readonly IMeterFactory _meterFactory = MeterFactories.Create();
    private readonly MetricCollector<long> _requests;
    private readonly RequestMetrics _metrics;
    private readonly RequestMetricsBehaviour<MetricsProbeRequest, Unit> _behaviour;

    public RequestMetricsBehaviourTests()
    {
        _requests = new MetricCollector<long>(_meterFactory, "Probe.Meter", "probe.requests");
        _metrics = new RequestMetrics(_meterFactory, "Probe.Meter", "probe");
        _behaviour = new RequestMetricsBehaviour<MetricsProbeRequest, Unit>(_metrics);
    }

    [Fact]
    public async Task Handle_NextSucceeds_RecordsSuccessOutcome()
    {
        await _behaviour.Handle(new MetricsProbeRequest(), _ => Unit.Task, TestContext.Current.CancellationToken);

        RecordedTags().Should().Contain(_metrics.RequestTag, nameof(MetricsProbeRequest)).And.Contain(_metrics.OutcomeTag, "Success");
    }

    [Fact]
    public async Task Handle_ValidationFails_RecordsValidationFailedAndRethrows()
    {
        var act = () => _behaviour.Handle(new MetricsProbeRequest(), _ => throw new ValidationBehaviourException(["A", "B"], ["a", "b"]), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<ValidationBehaviourException>()).Which.ErrorCode.Should().Be("A,B");
        RecordedTags().Should().Contain(_metrics.OutcomeTag, "VALIDATION_FAILED");
    }

    [Fact]
    public async Task Handle_CoreExceptionThrown_RecordsErrorCodeAndRethrows()
    {
        var act = () => _behaviour.Handle(new MetricsProbeRequest(), _ => throw new NotFoundCoreException("PROBE_NOT_FOUND"), TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<NotFoundCoreException>()).Which.ErrorCode.Should().Be("PROBE_NOT_FOUND");
        RecordedTags().Should().Contain(_metrics.OutcomeTag, "PROBE_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_UnexpectedException_RecordsUnhandledAndRethrows()
    {
        var act = () => _behaviour.Handle(new MetricsProbeRequest(), _ => throw new InvalidOperationException(), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
        RecordedTags().Should().Contain(_metrics.OutcomeTag, "UNHANDLED_EXCEPTION");
    }

    [Fact]
    public async Task Handle_Cancelled_RecordsCancelledAndRethrows()
    {
        var act = () => _behaviour.Handle(new MetricsProbeRequest(), _ => throw new OperationCanceledException(), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<OperationCanceledException>();
        RecordedTags().Should().Contain(_metrics.OutcomeTag, "CANCELLED");
    }

    public void Dispose() => _requests.Dispose();

    private IReadOnlyDictionary<string, object?> RecordedTags() => _requests.GetMeasurementSnapshot().Should().ContainSingle().Subject.Tags;
}
