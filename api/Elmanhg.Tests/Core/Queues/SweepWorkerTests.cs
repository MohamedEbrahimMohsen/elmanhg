using Core.Errors;
using Core.Queues;
using Elmanhg.Tests.Api.Workers;
using Elmanhg.Tests.Application.Features.Shared.Observability;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Elmanhg.Tests.Core.Queues;

public sealed class SweepWorkerTests
{
    private const string MeterName = "Probe.Meter";
    private static readonly ActivitySource ProbeSource = new("Probe.Sweep");
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly ManualTimeProvider _time = new();
    private readonly IMeterFactory _meterFactory = MeterFactories.Create();
    private readonly TaskCompletionSource _done = new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Fact]
    public async Task Execute_Disabled_EndsWithoutListing()
    {
        using var interval = Collector<double>("probe.job.interval");
        var listings = 0;
        using var worker = Worker(new SweepOptions { Enabled = false });
        worker.List = _ =>
        {
            listings++;
            return Task.FromResult<List<Guid>>([]);
        };

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await worker.ExecuteTask!.WaitAsync(WaitLimit, TestContext.Current.CancellationToken);

        worker.ExecuteTask.IsCompleted.Should().BeTrue();
        listings.Should().Be(0);
        interval.RecordObservableInstruments();
        interval.GetMeasurementSnapshot().Should().BeEmpty();
    }

    [Fact]
    public async Task Execute_Enabled_RegistersIntervalUnderJobName()
    {
        using var interval = Collector<double>("probe.job.interval");
        using var worker = Worker(new SweepOptions { IntervalSeconds = 45 });
        worker.List = _ => CompleteWith([]);

        await RunAsync(worker);
        interval.RecordObservableInstruments();

        interval.LastMeasurement!.Value.Should().Be(45);
        interval.LastMeasurement.Tags.Should().Contain("probe.job", ProbeSweepWorker.Name);
    }

    [Fact]
    public async Task Sweep_AllItemsSucceed_ProcessesEachAndRecordsSucceededRun()
    {
        using var runs = Collector<long>("probe.job.runs");
        using var items = Collector<long>("probe.job.items");
        Guid[] ids = [Guid.CreateVersion7(), Guid.CreateVersion7()];
        List<Guid> processed = [];
        using var worker = Worker(new SweepOptions());
        worker.List = _ => Task.FromResult<List<Guid>>([.. ids]);
        worker.Process = id =>
        {
            processed.Add(id);
            return CompleteOn(id == ids[1]);
        };

        await RunAsync(worker);
        await runs.WaitForMeasurementsAsync(1, WaitLimit);

        processed.Should().Equal(ids);
        runs.LastMeasurement!.Tags.Should().Contain("probe.job", ProbeSweepWorker.Name).And.Contain("probe.outcome", "Succeeded");
        items.GetMeasurementSnapshot().Should().ContainSingle().Which.Should().Match<CollectedMeasurement<long>>(x => x.Value == 2 && Equals(x.Tags["probe.outcome"], "Succeeded"));
        LoggedLevels().Should().BeEmpty();
    }

    [Fact]
    public async Task Sweep_ListingAndEachItem_ResolveSenderFromNewScopes()
    {
        using var runs = Collector<long>("probe.job.runs");
        Guid[] ids = [Guid.CreateVersion7(), Guid.CreateVersion7()];
        var resolutions = 0;
        var scopeFactory = new ServiceCollection().AddScoped(_ =>
        {
            Interlocked.Increment(ref resolutions);
            return _sender;
        }).BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        using var worker = Worker(new SweepOptions(), scopeFactory);
        worker.List = _ => Task.FromResult<List<Guid>>([.. ids]);
        worker.Process = id => CompleteOn(id == ids[1]);

        await RunAsync(worker);
        await runs.WaitForMeasurementsAsync(1, WaitLimit);

        resolutions.Should().Be(3);
    }

    [Fact]
    public async Task Sweep_ItemThrowsCoreException_LogsWarningAndRecordsFailureWithErrorCode()
    {
        Guid[] ids = [Guid.CreateVersion7(), Guid.CreateVersion7()];
        List<(Guid, string)> failures = [];
        List<Guid> processed = [];
        using var worker = Worker(new SweepOptions());
        worker.List = _ => Task.FromResult<List<Guid>>([.. ids]);
        worker.Process = id =>
        {
            processed.Add(id);
            return id == ids[0] ? Task.FromException(new ConflictCoreException("PROBE_FAILED")) : CompleteOn(true);
        };
        worker.RecordFailure = (id, errorCode) =>
        {
            failures.Add((id, errorCode));
            return Task.CompletedTask;
        };

        await RunAsync(worker);

        failures.Should().Equal((ids[0], "PROBE_FAILED"));
        LoggedLevels().Should().Equal(LogLevel.Warning);
        processed.Should().Equal(ids);
    }

    [Fact]
    public async Task Sweep_ItemThrowsPlainException_RecordsExceptionTypeName()
    {
        var id = Guid.CreateVersion7();
        string? recorded = null;
        using var worker = Worker(new SweepOptions());
        worker.List = _ => Task.FromResult<List<Guid>>([id]);
        worker.Process = _ => Task.FromException(new InvalidOperationException());
        worker.RecordFailure = (_, errorCode) =>
        {
            recorded = errorCode;
            return CompleteOn(true);
        };

        await RunAsync(worker);

        recorded.Should().Be("InvalidOperationException");
    }

    [Fact]
    public async Task Sweep_RecordingFailureThrows_LogsErrorAndContinues()
    {
        using var runs = Collector<long>("probe.job.runs");
        Guid[] ids = [Guid.CreateVersion7(), Guid.CreateVersion7()];
        List<Guid> processed = [];
        using var worker = Worker(new SweepOptions());
        worker.List = _ => Task.FromResult<List<Guid>>([.. ids]);
        worker.Process = id =>
        {
            processed.Add(id);
            return id == ids[0] ? Task.FromException(new InvalidOperationException()) : CompleteOn(true);
        };
        worker.RecordFailure = (_, _) => Task.FromException(new InvalidOperationException());

        await RunAsync(worker);
        await runs.WaitForMeasurementsAsync(1, WaitLimit);

        LoggedLevels().Should().Equal(LogLevel.Warning, LogLevel.Error);
        processed.Should().Equal(ids);
        runs.LastMeasurement!.Tags.Should().Contain("probe.outcome", "PartiallyFailed");
    }

    [Fact]
    public async Task Sweep_ListingThrows_LogsErrorAndRecordsFailedRun()
    {
        using var runs = Collector<long>("probe.job.runs");
        var processed = 0;
        using var worker = Worker(new SweepOptions());
        worker.List = _ =>
        {
            Complete();
            return Task.FromException<List<Guid>>(new InvalidOperationException());
        };
        worker.Process = _ =>
        {
            processed++;
            return Task.CompletedTask;
        };

        await RunAsync(worker);
        await runs.WaitForMeasurementsAsync(1, WaitLimit);

        LoggedLevels().Should().Equal(LogLevel.Error);
        runs.LastMeasurement!.Tags.Should().Contain("probe.job", ProbeSweepWorker.Name).And.Contain("probe.outcome", "Failed");
        processed.Should().Be(0);
    }

    [Fact]
    public async Task Sweep_FailedIds_AreDeferredUntilAShortBatch()
    {
        Guid[] ids = [Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7()];
        List<List<Guid>> deferred = [];
        Queue<List<Guid>> pages = new([[ids[0], ids[1]], [ids[2], ids[3]], [ids[4]], []]);
        List<Guid> processed = [];
        using var worker = Worker(new SweepOptions { BatchSize = 2 });
        worker.List = deferredIds =>
        {
            deferred.Add([.. deferredIds]);
            if (deferred.Count < 4)
            {
                _time.Tick();
            }
            else
            {
                Complete();
            }

            return Task.FromResult(pages.Dequeue());
        };
        worker.Process = id =>
        {
            processed.Add(id);
            return id == ids[4] ? Task.CompletedTask : Task.FromException(new InvalidOperationException());
        };

        await RunAsync(worker);

        deferred[0].Should().BeEmpty();
        deferred[1].Should().BeEquivalentTo([ids[0], ids[1]]);
        deferred[2].Should().BeEquivalentTo([ids[0], ids[1], ids[2], ids[3]]);
        deferred[3].Should().BeEmpty();
        processed.Should().ContainSingle(x => x == ids[4]);
    }

    [Fact]
    public async Task Sweep_BeforeListHook_RunsBeforeListingInsideTheRun()
    {
        using var runs = Collector<long>("probe.job.runs");
        List<string> calls = [];
        using var worker = Worker(new SweepOptions());
        worker.BeforeList = () =>
        {
            calls.Add("BeforeList");
            return Task.CompletedTask;
        };
        worker.List = _ =>
        {
            calls.Add("List");
            return CompleteWith([]);
        };

        await RunAsync(worker);
        await runs.WaitForMeasurementsAsync(1, WaitLimit);

        calls.Should().Equal("BeforeList", "List");
        runs.GetMeasurementSnapshot().Should().ContainSingle();
    }

    [Fact]
    public async Task Sweep_BeforeListHookThrows_LogsErrorAndKeepsTheLoopRunning()
    {
        var listings = 0;
        using var worker = Worker(new SweepOptions());
        worker.BeforeList = () => Task.FromException(new InvalidOperationException());
        worker.List = _ =>
        {
            if (++listings < 2)
            {
                _time.Tick();
                return Task.FromResult<List<Guid>>([]);
            }

            return CompleteWith([]);
        };

        await RunAsync(worker);

        listings.Should().Be(2);
        worker.ExecuteTask!.IsCompleted.Should().BeFalse();
        LoggedLevels().Should().Equal(LogLevel.Error, LogLevel.Error);
    }

    [Fact]
    public async Task Stop_DuringListing_EndsTheLoopWithoutLogging()
    {
        var listing = new TaskCompletionSource<List<Guid>>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var worker = Worker(new SweepOptions());
        worker.List = _ =>
        {
            Complete();
            return listing.Task;
        };
        await RunAsync(worker);

        var stop = worker.StopAsync(TestContext.Current.CancellationToken);
        listing.SetException(new OperationCanceledException());
        await stop.WaitAsync(WaitLimit, TestContext.Current.CancellationToken);

        worker.ExecuteTask!.IsCompleted.Should().BeTrue();
        LoggedLevels().Should().BeEmpty();
    }

    [Fact]
    public async Task Sweep_NoRecordFailureOverride_StillCountsFailedItem()
    {
        using var items = Collector<long>("probe.job.items");
        using var worker = Worker(new SweepOptions());
        worker.List = _ => Task.FromResult<List<Guid>>([Guid.CreateVersion7()]);
        worker.Process = _ =>
        {
            Complete();
            return Task.FromException(new InvalidOperationException());
        };

        await RunAsync(worker);
        await items.WaitForMeasurementsAsync(1, WaitLimit);

        items.GetMeasurementSnapshot().Should().ContainSingle().Which.Should().Match<CollectedMeasurement<long>>(x => x.Value == 1 && Equals(x.Tags["probe.outcome"], "Failed"));
        LoggedLevels().Should().Equal(LogLevel.Warning);
    }

    private ProbeSweepWorker Worker(SweepOptions options, IServiceScopeFactory? scopeFactory = null)
    {
        scopeFactory ??= new ServiceCollection().AddSingleton(_sender).BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        return new ProbeSweepWorker(scopeFactory, options, _time, _logger, new BackgroundJobMetrics(_meterFactory, _time, MeterName, "probe", ProbeSource));
    }

    private async Task RunAsync(ProbeSweepWorker worker)
    {
        await worker.StartAsync(TestContext.Current.CancellationToken);
        await _time.TimerCreated.WaitAsync(WaitLimit, TestContext.Current.CancellationToken);
        _time.Tick();
        await Task.WhenAny(_done.Task, worker.ExecuteTask!).WaitAsync(WaitLimit, TestContext.Current.CancellationToken);
        _done.Task.IsCompleted.Should().BeTrue();
    }

    private Task<List<Guid>> CompleteWith(List<Guid> ids)
    {
        Complete();
        return Task.FromResult(ids);
    }

    private Task CompleteOn(bool condition)
    {
        if (condition)
        {
            Complete();
        }

        return Task.CompletedTask;
    }

    private void Complete() => _done.TrySetResult();

    private MetricCollector<T> Collector<T>(string instrument) where T : struct => new(_meterFactory, MeterName, instrument);

    private List<LogLevel> LoggedLevels() => _logger.ReceivedCalls().Where(x => x.GetMethodInfo().Name == nameof(ILogger.Log)).Select(x => (LogLevel)x.GetArguments()[0]!).ToList();
}
