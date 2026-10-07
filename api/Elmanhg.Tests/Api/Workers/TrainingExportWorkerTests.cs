using Core.Errors;
using Elmanhg.Api.Workers;
using Elmanhg.Application.TrainingExports.FailTrainingExport;
using Elmanhg.Application.TrainingExports.GetDueTrainingExportIds;
using Elmanhg.Application.TrainingExports.RunTrainingExport;
using Elmanhg.Application.Exceptions;
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

public sealed class TrainingExportWorkerTests
{
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly ILogger<TrainingExportWorker> _logger = Substitute.For<ILogger<TrainingExportWorker>>();
    private readonly ManualTimeProvider _time = new();
    private readonly IMeterFactory _meterFactory = MeterFactories.Create();
    private readonly TaskCompletionSource _done = new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Fact]
    public async Task Sweep_DueExports_RunsEach()
    {
        var ids = StubDue();
        CompleteOnRun(ids[1]);

        using var worker = await RunAsync();

        await _sender.Received(1).Send(Arg.Is<RunTrainingExportCommand>(x => x.ExportId == ids[0]), Arg.Any<CancellationToken>());
        await _sender.Received(1).Send(Arg.Is<RunTrainingExportCommand>(x => x.ExportId == ids[1]), Arg.Any<CancellationToken>());
        await _sender.DidNotReceive().Send(Arg.Any<FailTrainingExportCommand>(), Arg.Any<CancellationToken>());
        LoggedLevels().Should().BeEmpty();
    }

    [Fact]
    public async Task Sweep_RunFails_LogsWarningAndRecordsFailureWithErrorCode()
    {
        var ids = StubDue();
        _sender.Send(Arg.Is<RunTrainingExportCommand>(x => x.ExportId == ids[0]), Arg.Any<CancellationToken>()).Returns(Task.FromException(new NotFoundCoreException(ErrorCodes.TrainingExportNotFound)));
        CompleteOnRun(ids[1]);

        using var worker = await RunAsync();

        worker.ExecuteTask!.IsCompleted.Should().BeFalse();
        LoggedLevels().Should().Equal(LogLevel.Warning);
        await _sender.Received(1).Send(Arg.Is<FailTrainingExportCommand>(x => x.ExportId == ids[0] && x.ErrorCode == "TRAINING_EXPORT_NOT_FOUND"), Arg.Any<CancellationToken>());
        await _sender.DidNotReceive().Send(Arg.Is<FailTrainingExportCommand>(x => x.ExportId == ids[1]), Arg.Any<CancellationToken>());
        await _sender.Received(1).Send(Arg.Is<RunTrainingExportCommand>(x => x.ExportId == ids[1]), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sweep_ClaimLostToAnotherRun_SkipsWithoutRecordingFailure()
    {
        var ids = StubDue();
        _sender.Send(Arg.Is<RunTrainingExportCommand>(x => x.ExportId == ids[0]), Arg.Any<CancellationToken>()).Returns(Task.FromException(new ConflictCoreException(ErrorCodes.TrainingExportModifiedConcurrently)));
        CompleteOnRun(ids[1]);

        using var worker = await RunAsync();

        LoggedLevels().Should().Equal(LogLevel.Information);
        await _sender.DidNotReceive().Send(Arg.Any<FailTrainingExportCommand>(), Arg.Any<CancellationToken>());
        await _sender.Received(1).Send(Arg.Is<RunTrainingExportCommand>(x => x.ExportId == ids[1]), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sweep_UnknownException_RecordsExceptionTypeName()
    {
        var ids = StubDue();
        _sender.Send(Arg.Is<RunTrainingExportCommand>(x => x.ExportId == ids[0]), Arg.Any<CancellationToken>()).Returns(Task.FromException(new InvalidOperationException()));
        CompleteOnRun(ids[1]);

        using var worker = await RunAsync();

        await _sender.Received(1).Send(Arg.Is<FailTrainingExportCommand>(x => x.ExportId == ids[0] && x.ErrorCode == "InvalidOperationException"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sweep_Disabled_NeverQueries()
    {
        using var worker = new TrainingExportWorker(ScopeFactory(), Options.Create(new TrainingExportsOptions { SweepEnabled = false }), _time, _logger, ElmanhgJobMetrics.Create(_meterFactory, _time));

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await worker.ExecuteTask!.WaitAsync(WaitLimit, TestContext.Current.CancellationToken);

        worker.ExecuteTask.IsCompleted.Should().BeTrue();
        await _sender.DidNotReceive().Send(Arg.Any<GetDueTrainingExportIdsQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Stop_DuringSweep_EndsTheLoopWithoutLogging()
    {
        _sender.Send(Arg.Any<GetDueTrainingExportIdsQuery>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            Complete();
            await Task.Delay(Timeout.Infinite, call.Arg<CancellationToken>());
            return new List<Guid>();
        });
        using var worker = await RunAsync();

        await worker.StopAsync(TestContext.Current.CancellationToken);

        worker.ExecuteTask!.IsCompleted.Should().BeTrue();
        LoggedLevels().Should().BeEmpty();
    }

    [Fact]
    public async Task Sweep_ListingFails_RecordsFailedRun()
    {
        using var runs = new MetricCollector<long>(_meterFactory, ElmanhgTelemetry.SourceName, "elmanhg.job.runs");
        _sender.Send(Arg.Any<GetDueTrainingExportIdsQuery>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            Complete();
            return Task.FromException<List<Guid>>(new InvalidOperationException());
        });

        using var worker = await RunAsync();
        await runs.WaitForMeasurementsAsync(1, WaitLimit);

        runs.LastMeasurement!.Tags.Should().Contain(ElmanhgJobMetrics.JobTag, "training-export").And.Contain(ElmanhgJobMetrics.OutcomeTag, "Failed");
    }

    private Guid[] StubDue()
    {
        Guid[] ids = [Guid.CreateVersion7(), Guid.CreateVersion7()];
        _sender.Send(Arg.Any<GetDueTrainingExportIdsQuery>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<List<Guid>>([.. ids]));
        return ids;
    }

    private async Task<TrainingExportWorker> RunAsync()
    {
        var worker = new TrainingExportWorker(ScopeFactory(), Options.Create(new TrainingExportsOptions()), _time, _logger, ElmanhgJobMetrics.Create(_meterFactory, _time));
        await worker.StartAsync(TestContext.Current.CancellationToken);
        await _time.TimerCreated.WaitAsync(WaitLimit, TestContext.Current.CancellationToken);
        _time.Tick();
        await Task.WhenAny(_done.Task, worker.ExecuteTask!).WaitAsync(WaitLimit, TestContext.Current.CancellationToken);
        _done.Task.IsCompleted.Should().BeTrue();
        return worker;
    }

    private IServiceScopeFactory ScopeFactory() => new ServiceCollection().AddSingleton(_sender).BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

    private void CompleteOnRun(Guid exportId) => _sender.Send(Arg.Is<RunTrainingExportCommand>(x => x.ExportId == exportId), Arg.Any<CancellationToken>()).Returns(_ =>
    {
        Complete();
        return Task.CompletedTask;
    });

    private void Complete() => _done.TrySetResult();

    private List<LogLevel> LoggedLevels() => _logger.ReceivedCalls().Where(x => x.GetMethodInfo().Name == nameof(ILogger.Log)).Select(x => (LogLevel)x.GetArguments()[0]!).ToList();
}
