using Elmanhg.Api.Workers;
using Elmanhg.Application.ContentRetrieval.GetStaleLessonContentIds;
using Elmanhg.Application.ContentRetrieval.ReindexLessonContent;
using Elmanhg.Application.Shared.Observability;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Tests.Application.Features.Shared.Observability;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Diagnostics.Metrics;

namespace Elmanhg.Tests.Api.Workers;

public sealed class LessonContentIndexWorkerTests
{
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly ILogger<LessonContentIndexWorker> _logger = Substitute.For<ILogger<LessonContentIndexWorker>>();
    private readonly ManualTimeProvider _time = new();
    private readonly IMeterFactory _meterFactory = MeterFactories.Create();
    private readonly TaskCompletionSource _done = new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Fact]
    public async Task Sweep_FailingLessons_LogsAndReindexesTheRest()
    {
        Guid[] ids = [Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7()];
        _sender.Send(Arg.Any<GetStaleLessonContentIdsQuery>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<List<Guid>>([.. ids]));
        FailReindex(ids[0], new InvalidOperationException());
        FailReindex(ids[1], new OperationCanceledException());
        _sender.Send(Arg.Is<ReindexLessonContentCommand>(x => x.LessonId == ids[2]), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            Complete();
            return Task.CompletedTask;
        });

        using var worker = await RunAsync(new ContentRetrievalOptions());

        worker.ExecuteTask!.IsCompleted.Should().BeFalse();
        LoggedLevels().Should().Equal(LogLevel.Warning, LogLevel.Warning);
        await _sender.Received(1).Send(Arg.Is<ReindexLessonContentCommand>(x => x.LessonId == ids[2]), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sweep_FailedIds_AreSkippedUntilTheBacklogEnds()
    {
        Guid[] ids = [Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7()];
        List<List<Guid>> excluded = [];
        Queue<List<Guid>> pages = new([[ids[0], ids[1]], [ids[2], ids[3]], [ids[4]], []]);
        _sender.Send(Arg.Any<GetStaleLessonContentIdsQuery>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            excluded.Add([.. call.Arg<GetStaleLessonContentIdsQuery>().ExcludedIds]);
            if (excluded.Count < 4)
            {
                _time.Tick();
            }
            else
            {
                Complete();
            }

            return Task.FromResult(pages.Dequeue());
        });
        _sender.Send(Arg.Any<ReindexLessonContentCommand>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(new InvalidOperationException()));
        _sender.Send(Arg.Is<ReindexLessonContentCommand>(x => x.LessonId == ids[4]), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        using var worker = await RunAsync(new ContentRetrievalOptions { IndexSweepBatchSize = 2 });

        excluded[0].Should().BeEmpty();
        excluded[1].Should().BeEquivalentTo([ids[0], ids[1]]);
        excluded[2].Should().BeEquivalentTo([ids[0], ids[1], ids[2], ids[3]]);
        excluded[3].Should().BeEmpty();
        await _sender.Received(1).Send(Arg.Is<ReindexLessonContentCommand>(x => x.LessonId == ids[4]), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Stop_DuringSweep_EndsTheLoopWithoutLogging()
    {
        _sender.Send(Arg.Any<GetStaleLessonContentIdsQuery>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            Complete();
            await Task.Delay(Timeout.Infinite, call.Arg<CancellationToken>());
            return new List<Guid>();
        });
        using var worker = await RunAsync(new ContentRetrievalOptions());

        await worker.StopAsync(TestContext.Current.CancellationToken);

        worker.ExecuteTask!.IsCompleted.Should().BeTrue();
        LoggedLevels().Should().BeEmpty();
    }

    [Fact]
    public async Task Execute_Disabled_EndsWithoutSweeping()
    {
        using var worker = new LessonContentIndexWorker(ScopeFactory(), Options.Create(new ContentRetrievalOptions { IndexSweepEnabled = false }), _time, _logger, ElmanhgJobMetrics.Create(_meterFactory, _time));

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await worker.ExecuteTask!.WaitAsync(WaitLimit, TestContext.Current.CancellationToken);

        worker.ExecuteTask.IsCompleted.Should().BeTrue();
        _sender.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task Sweep_AllReindexed_RecordsSucceededRunWithItemCount()
    {
        Guid[] ids = [Guid.CreateVersion7(), Guid.CreateVersion7()];
        using var runs = new MetricCollector<long>(_meterFactory, ElmanhgTelemetry.SourceName, "elmanhg.job.runs");
        using var items = new MetricCollector<long>(_meterFactory, ElmanhgTelemetry.SourceName, "elmanhg.job.items");
        _sender.Send(Arg.Any<GetStaleLessonContentIdsQuery>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<List<Guid>>([.. ids]));
        _sender.Send(Arg.Is<ReindexLessonContentCommand>(x => x.LessonId == ids[0]), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _sender.Send(Arg.Is<ReindexLessonContentCommand>(x => x.LessonId == ids[1]), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            Complete();
            return Task.CompletedTask;
        });

        using var worker = await RunAsync(new ContentRetrievalOptions());
        await runs.WaitForMeasurementsAsync(1, WaitLimit);

        runs.LastMeasurement!.Tags.Should().Contain(ElmanhgJobMetrics.JobTag, "lesson-content-index").And.Contain(ElmanhgJobMetrics.OutcomeTag, "Succeeded");
        items.GetMeasurementSnapshot().Should().ContainSingle().Which.Should().Match<CollectedMeasurement<long>>(x => x.Value == ids.Length && Equals(x.Tags[ElmanhgJobMetrics.OutcomeTag], "Succeeded"));
    }

    [Fact]
    public async Task Sweep_ListingFails_RecordsFailedRun()
    {
        using var runs = new MetricCollector<long>(_meterFactory, ElmanhgTelemetry.SourceName, "elmanhg.job.runs");
        _sender.Send(Arg.Any<GetStaleLessonContentIdsQuery>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            Complete();
            return Task.FromException<List<Guid>>(new InvalidOperationException());
        });

        using var worker = await RunAsync(new ContentRetrievalOptions());
        await runs.WaitForMeasurementsAsync(1, WaitLimit);

        runs.LastMeasurement!.Tags.Should().Contain(ElmanhgJobMetrics.JobTag, "lesson-content-index").And.Contain(ElmanhgJobMetrics.OutcomeTag, "Failed");
    }

    [Fact]
    public async Task Execute_Enabled_RegistersJobInterval()
    {
        using var interval = new MetricCollector<double>(_meterFactory, ElmanhgTelemetry.SourceName, "elmanhg.job.interval");
        _sender.Send(Arg.Any<GetStaleLessonContentIdsQuery>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            Complete();
            return Task.FromResult<List<Guid>>([]);
        });

        using var worker = await RunAsync(new ContentRetrievalOptions { IndexSweepIntervalSeconds = 45 });
        interval.RecordObservableInstruments();

        interval.LastMeasurement!.Value.Should().Be(45);
        interval.LastMeasurement.Tags.Should().Contain(ElmanhgJobMetrics.JobTag, "lesson-content-index");
    }

    private async Task<LessonContentIndexWorker> RunAsync(ContentRetrievalOptions options)
    {
        var worker = new LessonContentIndexWorker(ScopeFactory(), Options.Create(options), _time, _logger, ElmanhgJobMetrics.Create(_meterFactory, _time));
        await worker.StartAsync(TestContext.Current.CancellationToken);
        await _time.TimerCreated.WaitAsync(WaitLimit, TestContext.Current.CancellationToken);
        _time.Tick();
        await Task.WhenAny(_done.Task, worker.ExecuteTask!).WaitAsync(WaitLimit, TestContext.Current.CancellationToken);
        _done.Task.IsCompleted.Should().BeTrue();
        return worker;
    }

    private IServiceScopeFactory ScopeFactory() => new ServiceCollection().AddSingleton(_sender).BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

    private void FailReindex(Guid lessonId, Exception exception) => _sender.Send(Arg.Is<ReindexLessonContentCommand>(x => x.LessonId == lessonId), Arg.Any<CancellationToken>()).Returns(Task.FromException(exception));

    private void Complete() => _done.TrySetResult();

    private List<LogLevel> LoggedLevels() => _logger.ReceivedCalls().Where(x => x.GetMethodInfo().Name == nameof(ILogger.Log)).Select(x => (LogLevel)x.GetArguments()[0]!).ToList();
}
