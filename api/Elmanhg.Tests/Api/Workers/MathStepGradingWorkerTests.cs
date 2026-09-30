using Core.Errors;
using Elmanhg.Api.Workers;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.MathStepGrading.ApplyMathStepGrade;
using Elmanhg.Application.MathStepGrading.CheckMathStepAnswer;
using Elmanhg.Application.MathStepGrading.FailMathStepGrade;
using Elmanhg.Application.MathStepGrading.GetDueMathStepGradeIds;
using Elmanhg.Application.MathStepGrading.GradeMathSteps;
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

public sealed class MathStepGradingWorkerTests
{
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly ILogger<MathStepGradingWorker> _logger = Substitute.For<ILogger<MathStepGradingWorker>>();
    private readonly ManualTimeProvider _time = new();
    private readonly IMeterFactory _meterFactory = MeterFactories.Create();
    private readonly TaskCompletionSource _done = new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Fact]
    public async Task Sweep_DueGrades_ChecksGradesAndAppliesEach()
    {
        var ids = StubDue();
        CompleteOnApply(ids[1]);

        using var worker = await RunAsync();

        Received.InOrder(() =>
        {
            _sender.Send(Arg.Is<CheckMathStepAnswerCommand>(x => x.MathStepGradeId == ids[0]), Arg.Any<CancellationToken>());
            _sender.Send(Arg.Is<GradeMathStepsCommand>(x => x.MathStepGradeId == ids[0]), Arg.Any<CancellationToken>());
            _sender.Send(Arg.Is<ApplyMathStepGradeCommand>(x => x.MathStepGradeId == ids[0]), Arg.Any<CancellationToken>());
            _sender.Send(Arg.Is<CheckMathStepAnswerCommand>(x => x.MathStepGradeId == ids[1]), Arg.Any<CancellationToken>());
            _sender.Send(Arg.Is<GradeMathStepsCommand>(x => x.MathStepGradeId == ids[1]), Arg.Any<CancellationToken>());
            _sender.Send(Arg.Is<ApplyMathStepGradeCommand>(x => x.MathStepGradeId == ids[1]), Arg.Any<CancellationToken>());
        });
        await _sender.DidNotReceive().Send(Arg.Any<FailMathStepGradeCommand>(), Arg.Any<CancellationToken>());
        LoggedLevels().Should().BeEmpty();
    }

    [Fact]
    public async Task Sweep_CheckFails_RecordsFailureWithErrorCode()
    {
        var ids = StubDue();
        _sender.Send(Arg.Is<CheckMathStepAnswerCommand>(x => x.MathStepGradeId == ids[0]), Arg.Any<CancellationToken>()).Returns(Task.FromException(new ServiceUnavailableCoreException(ErrorCodes.MathCheckUnavailable)));
        CompleteOnApply(ids[1]);

        using var worker = await RunAsync();

        worker.ExecuteTask!.IsCompleted.Should().BeFalse();
        LoggedLevels().Should().Equal(LogLevel.Warning);
        await _sender.Received(1).Send(Arg.Is<FailMathStepGradeCommand>(x => x.MathStepGradeId == ids[0] && x.ErrorCode == "MATH_CHECK_UNAVAILABLE"), Arg.Any<CancellationToken>());
        await _sender.DidNotReceive().Send(Arg.Is<GradeMathStepsCommand>(x => x.MathStepGradeId == ids[0]), Arg.Any<CancellationToken>());
        await _sender.Received(1).Send(Arg.Is<GradeMathStepsCommand>(x => x.MathStepGradeId == ids[1]), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sweep_UnknownException_RecordsExceptionTypeName()
    {
        var ids = StubDue();
        _sender.Send(Arg.Is<GradeMathStepsCommand>(x => x.MathStepGradeId == ids[0]), Arg.Any<CancellationToken>()).Returns(Task.FromException(new InvalidOperationException()));
        CompleteOnApply(ids[1]);

        using var worker = await RunAsync();

        await _sender.Received(1).Send(Arg.Is<FailMathStepGradeCommand>(x => x.MathStepGradeId == ids[0] && x.ErrorCode == "InvalidOperationException"), Arg.Any<CancellationToken>());
        await _sender.DidNotReceive().Send(Arg.Is<ApplyMathStepGradeCommand>(x => x.MathStepGradeId == ids[0]), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sweep_Disabled_NeverQueries()
    {
        using var worker = new MathStepGradingWorker(ScopeFactory(), Options.Create(new MathStepGradingOptions { SweepEnabled = false }), _time, _logger, new BackgroundJobMetrics(_meterFactory, _time));

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await worker.ExecuteTask!.WaitAsync(WaitLimit, TestContext.Current.CancellationToken);

        worker.ExecuteTask.IsCompleted.Should().BeTrue();
        await _sender.DidNotReceive().Send(Arg.Any<GetDueMathStepGradeIdsQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sweep_ListingFails_RecordsFailedRun()
    {
        using var runs = new MetricCollector<long>(_meterFactory, ElmanhgTelemetry.SourceName, "elmanhg.job.runs");
        _sender.Send(Arg.Any<GetDueMathStepGradeIdsQuery>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            Complete();
            return Task.FromException<List<Guid>>(new InvalidOperationException());
        });

        using var worker = await RunAsync();
        await runs.WaitForMeasurementsAsync(1, WaitLimit);

        runs.LastMeasurement!.Tags.Should().Contain(BackgroundJobMetrics.JobTag, "math-step-grading").And.Contain(BackgroundJobMetrics.OutcomeTag, "Failed");
    }

    private Guid[] StubDue()
    {
        Guid[] ids = [Guid.CreateVersion7(), Guid.CreateVersion7()];
        _sender.Send(Arg.Any<GetDueMathStepGradeIdsQuery>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<List<Guid>>([.. ids]));
        return ids;
    }

    private async Task<MathStepGradingWorker> RunAsync()
    {
        var worker = new MathStepGradingWorker(ScopeFactory(), Options.Create(new MathStepGradingOptions()), _time, _logger, new BackgroundJobMetrics(_meterFactory, _time));
        await worker.StartAsync(TestContext.Current.CancellationToken);
        await _time.TimerCreated.WaitAsync(WaitLimit, TestContext.Current.CancellationToken);
        _time.Tick();
        await Task.WhenAny(_done.Task, worker.ExecuteTask!).WaitAsync(WaitLimit, TestContext.Current.CancellationToken);
        _done.Task.IsCompleted.Should().BeTrue();
        return worker;
    }

    private IServiceScopeFactory ScopeFactory() => new ServiceCollection().AddSingleton(_sender).BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

    private void CompleteOnApply(Guid gradeId) => _sender.Send(Arg.Is<ApplyMathStepGradeCommand>(x => x.MathStepGradeId == gradeId), Arg.Any<CancellationToken>()).Returns(_ =>
    {
        Complete();
        return Task.CompletedTask;
    });

    private void Complete() => _done.TrySetResult();

    private List<LogLevel> LoggedLevels() => _logger.ReceivedCalls().Where(x => x.GetMethodInfo().Name == nameof(ILogger.Log)).Select(x => (LogLevel)x.GetArguments()[0]!).ToList();
}
