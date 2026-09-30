using Core.Errors;
using Elmanhg.Api.Workers;
using Elmanhg.Application.EssayGrading.ApplyEssayGrade;
using Elmanhg.Application.EssayGrading.FailEssayGrade;
using Elmanhg.Application.EssayGrading.GetDueEssayGradeIds;
using Elmanhg.Application.EssayGrading.GradeEssay;
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

public sealed class EssayGradingWorkerTests
{
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly ILogger<EssayGradingWorker> _logger = Substitute.For<ILogger<EssayGradingWorker>>();
    private readonly ManualTimeProvider _time = new();
    private readonly IMeterFactory _meterFactory = MeterFactories.Create();
    private readonly TaskCompletionSource _done = new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Fact]
    public async Task Sweep_DueGrades_GradesEach()
    {
        var ids = StubDue();
        CompleteOnGrade(ids[1]);

        using var worker = await RunAsync();

        await _sender.Received(1).Send(Arg.Is<GradeEssayCommand>(x => x.EssayGradeId == ids[0]), Arg.Any<CancellationToken>());
        await _sender.Received(1).Send(Arg.Is<GradeEssayCommand>(x => x.EssayGradeId == ids[1]), Arg.Any<CancellationToken>());
        await _sender.DidNotReceive().Send(Arg.Any<FailEssayGradeCommand>(), Arg.Any<CancellationToken>());
        LoggedLevels().Should().BeEmpty();
    }

    [Fact]
    public async Task Sweep_GradingFails_LogsWarningAndRecordsFailureWithErrorCode()
    {
        var ids = StubDue();
        _sender.Send(Arg.Is<GradeEssayCommand>(x => x.EssayGradeId == ids[0]), Arg.Any<CancellationToken>()).Returns(Task.FromException(new ServiceUnavailableCoreException(ErrorCodes.EssayGradingUnavailable)));
        CompleteOnGrade(ids[1]);

        using var worker = await RunAsync();

        worker.ExecuteTask!.IsCompleted.Should().BeFalse();
        LoggedLevels().Should().Equal(LogLevel.Warning);
        await _sender.Received(1).Send(Arg.Is<FailEssayGradeCommand>(x => x.EssayGradeId == ids[0] && x.ErrorCode == "ESSAY_GRADING_UNAVAILABLE"), Arg.Any<CancellationToken>());
        await _sender.DidNotReceive().Send(Arg.Is<FailEssayGradeCommand>(x => x.EssayGradeId == ids[1]), Arg.Any<CancellationToken>());
        await _sender.Received(1).Send(Arg.Is<GradeEssayCommand>(x => x.EssayGradeId == ids[1]), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sweep_UnknownException_RecordsExceptionTypeName()
    {
        var ids = StubDue();
        _sender.Send(Arg.Is<GradeEssayCommand>(x => x.EssayGradeId == ids[0]), Arg.Any<CancellationToken>()).Returns(Task.FromException(new InvalidOperationException()));
        CompleteOnGrade(ids[1]);

        using var worker = await RunAsync();

        await _sender.Received(1).Send(Arg.Is<FailEssayGradeCommand>(x => x.EssayGradeId == ids[0] && x.ErrorCode == "InvalidOperationException"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sweep_Disabled_NeverQueries()
    {
        using var worker = new EssayGradingWorker(ScopeFactory(), Options.Create(new EssayGradingOptions { SweepEnabled = false }), _time, _logger, new BackgroundJobMetrics(_meterFactory, _time));

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await worker.ExecuteTask!.WaitAsync(WaitLimit, TestContext.Current.CancellationToken);

        worker.ExecuteTask.IsCompleted.Should().BeTrue();
        await _sender.DidNotReceive().Send(Arg.Any<GetDueEssayGradeIdsQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Stop_DuringSweep_EndsTheLoopWithoutLogging()
    {
        _sender.Send(Arg.Any<GetDueEssayGradeIdsQuery>(), Arg.Any<CancellationToken>()).Returns(async call =>
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
        _sender.Send(Arg.Any<GetDueEssayGradeIdsQuery>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            Complete();
            return Task.FromException<List<Guid>>(new InvalidOperationException());
        });

        using var worker = await RunAsync();
        await runs.WaitForMeasurementsAsync(1, WaitLimit);

        runs.LastMeasurement!.Tags.Should().Contain(BackgroundJobMetrics.JobTag, "essay-grading").And.Contain(BackgroundJobMetrics.OutcomeTag, "Failed");
    }

    [Fact]
    public async Task Sweep_DueGrade_AppliesAfterGrading()
    {
        var ids = StubDue();
        _sender.Send(Arg.Is<ApplyEssayGradeCommand>(x => x.EssayGradeId == ids[1]), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            Complete();
            return Task.CompletedTask;
        });

        using var worker = await RunAsync();

        Received.InOrder(() =>
        {
            _sender.Send(Arg.Is<GradeEssayCommand>(x => x.EssayGradeId == ids[0]), Arg.Any<CancellationToken>());
            _sender.Send(Arg.Is<ApplyEssayGradeCommand>(x => x.EssayGradeId == ids[0]), Arg.Any<CancellationToken>());
            _sender.Send(Arg.Is<GradeEssayCommand>(x => x.EssayGradeId == ids[1]), Arg.Any<CancellationToken>());
            _sender.Send(Arg.Is<ApplyEssayGradeCommand>(x => x.EssayGradeId == ids[1]), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Sweep_ApplyConflicts_RecordsFailureWithoutRegrading()
    {
        var ids = StubDue();
        _sender.Send(Arg.Is<ApplyEssayGradeCommand>(x => x.EssayGradeId == ids[0]), Arg.Any<CancellationToken>()).Returns(Task.FromException(new ConflictCoreException(ErrorCodes.SessionModifiedConcurrently)));
        CompleteOnGrade(ids[1]);

        using var worker = await RunAsync();

        await _sender.Received(1).Send(Arg.Is<GradeEssayCommand>(x => x.EssayGradeId == ids[0]), Arg.Any<CancellationToken>());
        await _sender.Received(1).Send(Arg.Is<FailEssayGradeCommand>(x => x.EssayGradeId == ids[0] && x.ErrorCode == ErrorCodes.SessionModifiedConcurrently), Arg.Any<CancellationToken>());
    }

    private Guid[] StubDue()
    {
        Guid[] ids = [Guid.CreateVersion7(), Guid.CreateVersion7()];
        _sender.Send(Arg.Any<GetDueEssayGradeIdsQuery>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<List<Guid>>([.. ids]));
        return ids;
    }

    private async Task<EssayGradingWorker> RunAsync()
    {
        var worker = new EssayGradingWorker(ScopeFactory(), Options.Create(new EssayGradingOptions()), _time, _logger, new BackgroundJobMetrics(_meterFactory, _time));
        await worker.StartAsync(TestContext.Current.CancellationToken);
        await _time.TimerCreated.WaitAsync(WaitLimit, TestContext.Current.CancellationToken);
        _time.Tick();
        await Task.WhenAny(_done.Task, worker.ExecuteTask!).WaitAsync(WaitLimit, TestContext.Current.CancellationToken);
        _done.Task.IsCompleted.Should().BeTrue();
        return worker;
    }

    private IServiceScopeFactory ScopeFactory() => new ServiceCollection().AddSingleton(_sender).BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

    private void CompleteOnGrade(Guid gradeId) => _sender.Send(Arg.Is<GradeEssayCommand>(x => x.EssayGradeId == gradeId), Arg.Any<CancellationToken>()).Returns(_ =>
    {
        Complete();
        return Task.CompletedTask;
    });

    private void Complete() => _done.TrySetResult();

    private List<LogLevel> LoggedLevels() => _logger.ReceivedCalls().Where(x => x.GetMethodInfo().Name == nameof(ILogger.Log)).Select(x => (LogLevel)x.GetArguments()[0]!).ToList();
}
