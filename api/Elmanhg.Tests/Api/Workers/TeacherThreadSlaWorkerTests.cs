using Elmanhg.Api.Workers;
using Elmanhg.Application.Shared.Observability;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.TeacherThreads.GetDueSlaThreadIds;
using Elmanhg.Application.TeacherThreads.ProcessTeacherThreadSla;
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

public sealed class TeacherThreadSlaWorkerTests
{
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly ILogger<TeacherThreadSlaWorker> _logger = Substitute.For<ILogger<TeacherThreadSlaWorker>>();
    private readonly ManualTimeProvider _time = new();
    private readonly IMeterFactory _meterFactory = MeterFactories.Create();
    private readonly TaskCompletionSource _done = new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Fact]
    public async Task Sweep_DueThreads_ProcessesEach()
    {
        Guid[] ids = [Guid.CreateVersion7(), Guid.CreateVersion7()];
        _sender.Send(Arg.Any<GetDueSlaThreadIdsQuery>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<List<Guid>>([.. ids]));
        _sender.Send(Arg.Is<ProcessTeacherThreadSlaCommand>(x => x.ThreadId == ids[0]), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _sender.Send(Arg.Is<ProcessTeacherThreadSlaCommand>(x => x.ThreadId == ids[1]), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            Complete();
            return Task.CompletedTask;
        });

        using var worker = await RunAsync(new AskTeacherOptions());

        await _sender.Received(1).Send(Arg.Is<ProcessTeacherThreadSlaCommand>(x => x.ThreadId == ids[0]), Arg.Any<CancellationToken>());
        await _sender.Received(1).Send(Arg.Is<ProcessTeacherThreadSlaCommand>(x => x.ThreadId == ids[1]), Arg.Any<CancellationToken>());
        LoggedLevels().Should().BeEmpty();
    }

    [Fact]
    public async Task Sweep_ProcessingFails_LogsWarningAndRecordsPartiallyFailedRun()
    {
        Guid[] ids = [Guid.CreateVersion7(), Guid.CreateVersion7()];
        using var runs = new MetricCollector<long>(_meterFactory, ElmanhgTelemetry.SourceName, "elmanhg.job.runs");
        _sender.Send(Arg.Any<GetDueSlaThreadIdsQuery>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<List<Guid>>([.. ids]));
        _sender.Send(Arg.Is<ProcessTeacherThreadSlaCommand>(x => x.ThreadId == ids[0]), Arg.Any<CancellationToken>()).Returns(Task.FromException(new InvalidOperationException()));
        _sender.Send(Arg.Is<ProcessTeacherThreadSlaCommand>(x => x.ThreadId == ids[1]), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            Complete();
            return Task.CompletedTask;
        });

        using var worker = await RunAsync(new AskTeacherOptions());
        await runs.WaitForMeasurementsAsync(1, WaitLimit);

        worker.ExecuteTask!.IsCompleted.Should().BeFalse();
        LoggedLevels().Should().Equal(LogLevel.Warning);
        runs.LastMeasurement!.Tags.Should().Contain(BackgroundJobMetrics.JobTag, "ask-teacher-sla").And.Contain(BackgroundJobMetrics.OutcomeTag, "PartiallyFailed");
    }

    [Fact]
    public async Task Sweep_FailedIds_AreSkippedUntilTheBacklogEnds()
    {
        Guid[] ids = [Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7()];
        List<List<Guid>> excluded = [];
        Queue<List<Guid>> pages = new([[ids[0], ids[1]], [ids[2], ids[3]], [ids[4]], []]);
        _sender.Send(Arg.Any<GetDueSlaThreadIdsQuery>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            excluded.Add([.. call.Arg<GetDueSlaThreadIdsQuery>().ExcludedIds]);
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
        _sender.Send(Arg.Any<ProcessTeacherThreadSlaCommand>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(new InvalidOperationException()));
        _sender.Send(Arg.Is<ProcessTeacherThreadSlaCommand>(x => x.ThreadId == ids[4]), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        using var worker = await RunAsync(new AskTeacherOptions { SlaSweepBatchSize = 2 });

        excluded[0].Should().BeEmpty();
        excluded[1].Should().BeEquivalentTo([ids[0], ids[1]]);
        excluded[2].Should().BeEquivalentTo([ids[0], ids[1], ids[2], ids[3]]);
        excluded[3].Should().BeEmpty();
        await _sender.Received(1).Send(Arg.Is<ProcessTeacherThreadSlaCommand>(x => x.ThreadId == ids[4]), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_Disabled_EndsWithoutSweeping()
    {
        using var worker = new TeacherThreadSlaWorker(ScopeFactory(), Options.Create(new AskTeacherOptions { SlaSweepEnabled = false }), _time, _logger, new BackgroundJobMetrics(_meterFactory, _time));

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await worker.ExecuteTask!.WaitAsync(WaitLimit, TestContext.Current.CancellationToken);

        worker.ExecuteTask.IsCompleted.Should().BeTrue();
        _sender.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task Sweep_ListingFails_RecordsFailedRun()
    {
        using var runs = new MetricCollector<long>(_meterFactory, ElmanhgTelemetry.SourceName, "elmanhg.job.runs");
        _sender.Send(Arg.Any<GetDueSlaThreadIdsQuery>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            Complete();
            return Task.FromException<List<Guid>>(new InvalidOperationException());
        });

        using var worker = await RunAsync(new AskTeacherOptions());
        await runs.WaitForMeasurementsAsync(1, WaitLimit);

        runs.LastMeasurement!.Tags.Should().Contain(BackgroundJobMetrics.JobTag, "ask-teacher-sla").And.Contain(BackgroundJobMetrics.OutcomeTag, "Failed");
        LoggedLevels().Should().Equal(LogLevel.Error);
    }

    private async Task<TeacherThreadSlaWorker> RunAsync(AskTeacherOptions options)
    {
        var worker = new TeacherThreadSlaWorker(ScopeFactory(), Options.Create(options), _time, _logger, new BackgroundJobMetrics(_meterFactory, _time));
        await worker.StartAsync(TestContext.Current.CancellationToken);
        await _time.TimerCreated.WaitAsync(WaitLimit, TestContext.Current.CancellationToken);
        _time.Tick();
        await Task.WhenAny(_done.Task, worker.ExecuteTask!).WaitAsync(WaitLimit, TestContext.Current.CancellationToken);
        _done.Task.IsCompleted.Should().BeTrue();
        return worker;
    }

    private IServiceScopeFactory ScopeFactory() => new ServiceCollection().AddSingleton(_sender).BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

    private void Complete() => _done.TrySetResult();

    private List<LogLevel> LoggedLevels() => _logger.ReceivedCalls().Where(x => x.GetMethodInfo().Name == nameof(ILogger.Log)).Select(x => (LogLevel)x.GetArguments()[0]!).ToList();
}
