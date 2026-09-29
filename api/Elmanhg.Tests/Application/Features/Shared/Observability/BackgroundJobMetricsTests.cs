using Elmanhg.Application.Shared.Observability;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using NSubstitute;
using System.Diagnostics.Metrics;

namespace Elmanhg.Tests.Application.Features.Shared.Observability;

public sealed class BackgroundJobMetricsTests
{
    private const string Job = "probe-job";
    private static readonly DateTimeOffset RegisteredAt = new(2026, 9, 29, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CompletedAt = RegisteredAt.AddMinutes(5);
    private readonly IMeterFactory _meterFactory = MeterFactories.Create();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly BackgroundJobMetrics _metrics;

    public BackgroundJobMetricsTests()
    {
        _timeProvider.GetUtcNow().Returns(RegisteredAt);
        _metrics = new BackgroundJobMetrics(_meterFactory, _timeProvider);
    }

    [Fact]
    public void Run_AllItemsSucceed_RecordsSucceededRunItemsAndDuration()
    {
        using var runs = Collector<long>("elmanhg.job.runs");
        using var items = Collector<long>("elmanhg.job.items");
        using var duration = Collector<double>("elmanhg.job.duration");

        using (var run = _metrics.StartRun(Job))
        {
            run.ItemSucceeded();
            run.ItemSucceeded();
        }

        runs.GetMeasurementSnapshot().Should().ContainSingle().Which.Tags.Should().Contain(BackgroundJobMetrics.JobTag, Job).And.Contain(BackgroundJobMetrics.OutcomeTag, "Succeeded");
        var item = items.GetMeasurementSnapshot().Should().ContainSingle().Subject;
        item.Value.Should().Be(2);
        item.Tags.Should().Contain(BackgroundJobMetrics.OutcomeTag, "Succeeded");
        duration.GetMeasurementSnapshot().Should().ContainSingle().Which.Value.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void Run_SomeItemsFail_RecordsPartiallyFailedRun()
    {
        using var runs = Collector<long>("elmanhg.job.runs");
        using var items = Collector<long>("elmanhg.job.items");

        using (var run = _metrics.StartRun(Job))
        {
            run.ItemSucceeded();
            run.ItemFailed();
        }

        runs.LastMeasurement!.Tags.Should().Contain(BackgroundJobMetrics.OutcomeTag, "PartiallyFailed");
        items.GetMeasurementSnapshot().Select(x => (x.Tags[BackgroundJobMetrics.OutcomeTag], x.Value)).Should().BeEquivalentTo([((object?)"Succeeded", 1L), ((object?)"Failed", 1L)]);
    }

    [Fact]
    public void Run_ListingFailed_RecordsFailedRunAndKeepsLastSuccess()
    {
        using var runs = Collector<long>("elmanhg.job.runs");
        using var lastSuccess = Collector<long>("elmanhg.job.last_success");
        _metrics.Register(Job, TimeSpan.FromSeconds(60));
        _timeProvider.GetUtcNow().Returns(CompletedAt);

        using (var run = _metrics.StartRun(Job))
        {
            run.MarkListingFailed();
        }

        lastSuccess.RecordObservableInstruments();
        runs.LastMeasurement!.Tags.Should().Contain(BackgroundJobMetrics.OutcomeTag, "Failed");
        lastSuccess.LastMeasurement!.Value.Should().Be(RegisteredAt.ToUnixTimeSeconds());
    }

    [Fact]
    public void Register_ReportsIntervalAndInitialLastSuccess()
    {
        using var interval = Collector<double>("elmanhg.job.interval");
        using var lastSuccess = Collector<long>("elmanhg.job.last_success");

        _metrics.Register(Job, TimeSpan.FromSeconds(60));

        interval.RecordObservableInstruments();
        lastSuccess.RecordObservableInstruments();
        interval.LastMeasurement!.Value.Should().Be(60);
        interval.LastMeasurement.Tags.Should().Contain(BackgroundJobMetrics.JobTag, Job);
        lastSuccess.LastMeasurement!.Value.Should().Be(RegisteredAt.ToUnixTimeSeconds());
        lastSuccess.LastMeasurement.Tags.Should().Contain(BackgroundJobMetrics.JobTag, Job);
    }

    [Fact]
    public void Run_Succeeded_AdvancesLastSuccessToCompletionTime()
    {
        using var lastSuccess = Collector<long>("elmanhg.job.last_success");
        _timeProvider.GetUtcNow().Returns(RegisteredAt, CompletedAt);
        _metrics.Register(Job, TimeSpan.FromSeconds(60));

        _metrics.StartRun(Job).Dispose();

        lastSuccess.RecordObservableInstruments();
        lastSuccess.LastMeasurement!.Value.Should().Be(CompletedAt.ToUnixTimeSeconds());
    }

    [Fact]
    public void Dispose_CalledTwice_RecordsOneRun()
    {
        using var runs = Collector<long>("elmanhg.job.runs");
        var run = _metrics.StartRun(Job);

        run.Dispose();
        run.Dispose();

        runs.GetMeasurementSnapshot().Sum(x => x.Value).Should().Be(1);
    }

    private MetricCollector<T> Collector<T>(string instrument) where T : struct => new(_meterFactory, ElmanhgTelemetry.SourceName, instrument);
}
