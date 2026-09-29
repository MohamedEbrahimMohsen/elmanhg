using Elmanhg.Api.Workers;
using Elmanhg.Application.Shared.Observability;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.TeacherInbox.FailVoiceDraftTranscription;
using Elmanhg.Application.TeacherInbox.GetDueVoiceDraftIds;
using Elmanhg.Application.TeacherInbox.TranscribeVoiceDraft;
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

public sealed class TeacherVoiceTranscriptionWorkerTests
{
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly ILogger<TeacherVoiceTranscriptionWorker> _logger = Substitute.For<ILogger<TeacherVoiceTranscriptionWorker>>();
    private readonly ManualTimeProvider _time = new();
    private readonly IMeterFactory _meterFactory = MeterFactories.Create();
    private readonly TaskCompletionSource _done = new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Fact]
    public async Task Sweep_DueDrafts_TranscribesEach()
    {
        Guid[] ids = [Guid.CreateVersion7(), Guid.CreateVersion7()];
        _sender.Send(Arg.Any<GetDueVoiceDraftIdsQuery>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<List<Guid>>([.. ids]));
        CompleteOnTranscribe(ids[1]);

        using var worker = await RunAsync();

        await _sender.Received(1).Send(Arg.Is<TranscribeVoiceDraftCommand>(x => x.DraftId == ids[0]), Arg.Any<CancellationToken>());
        await _sender.Received(1).Send(Arg.Is<TranscribeVoiceDraftCommand>(x => x.DraftId == ids[1]), Arg.Any<CancellationToken>());
        await _sender.DidNotReceive().Send(Arg.Any<FailVoiceDraftTranscriptionCommand>(), Arg.Any<CancellationToken>());
        LoggedLevels().Should().BeEmpty();
    }

    [Fact]
    public async Task Sweep_TranscriptionFails_LogsWarningAndRecordsFailure()
    {
        Guid[] ids = [Guid.CreateVersion7(), Guid.CreateVersion7()];
        _sender.Send(Arg.Any<GetDueVoiceDraftIdsQuery>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<List<Guid>>([.. ids]));
        _sender.Send(Arg.Is<TranscribeVoiceDraftCommand>(x => x.DraftId == ids[0]), Arg.Any<CancellationToken>()).Returns(Task.FromException(new InvalidOperationException()));
        CompleteOnTranscribe(ids[1]);

        using var worker = await RunAsync();

        worker.ExecuteTask!.IsCompleted.Should().BeFalse();
        LoggedLevels().Should().Equal(LogLevel.Warning);
        await _sender.Received(1).Send(Arg.Is<FailVoiceDraftTranscriptionCommand>(x => x.DraftId == ids[0]), Arg.Any<CancellationToken>());
        await _sender.DidNotReceive().Send(Arg.Is<FailVoiceDraftTranscriptionCommand>(x => x.DraftId == ids[1]), Arg.Any<CancellationToken>());
        await _sender.Received(1).Send(Arg.Is<TranscribeVoiceDraftCommand>(x => x.DraftId == ids[1]), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sweep_Disabled_NeverQueries()
    {
        using var worker = new TeacherVoiceTranscriptionWorker(ScopeFactory(), Options.Create(new AskTeacherOptions { TranscriptionSweepEnabled = false }), _time, _logger, new BackgroundJobMetrics(_meterFactory, _time));

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await worker.ExecuteTask!.WaitAsync(WaitLimit, TestContext.Current.CancellationToken);

        worker.ExecuteTask.IsCompleted.Should().BeTrue();
        await _sender.DidNotReceive().Send(Arg.Any<GetDueVoiceDraftIdsQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Stop_DuringSweep_EndsTheLoopWithoutLogging()
    {
        _sender.Send(Arg.Any<GetDueVoiceDraftIdsQuery>(), Arg.Any<CancellationToken>()).Returns(async call =>
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
    public async Task Sweep_OneTranscriptionFails_RecordsPartiallyFailedRun()
    {
        Guid[] ids = [Guid.CreateVersion7(), Guid.CreateVersion7()];
        using var runs = new MetricCollector<long>(_meterFactory, ElmanhgTelemetry.SourceName, "elmanhg.job.runs");
        using var items = new MetricCollector<long>(_meterFactory, ElmanhgTelemetry.SourceName, "elmanhg.job.items");
        _sender.Send(Arg.Any<GetDueVoiceDraftIdsQuery>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<List<Guid>>([.. ids]));
        _sender.Send(Arg.Is<TranscribeVoiceDraftCommand>(x => x.DraftId == ids[0]), Arg.Any<CancellationToken>()).Returns(Task.FromException(new InvalidOperationException()));
        CompleteOnTranscribe(ids[1]);

        using var worker = await RunAsync();
        await runs.WaitForMeasurementsAsync(1, WaitLimit);

        runs.LastMeasurement!.Tags.Should().Contain(BackgroundJobMetrics.JobTag, "teacher-voice-transcription").And.Contain(BackgroundJobMetrics.OutcomeTag, "PartiallyFailed");
        items.GetMeasurementSnapshot().Select(x => (x.Value, x.Tags[BackgroundJobMetrics.OutcomeTag])).Should().BeEquivalentTo([(1L, (object?)"Succeeded"), (1L, (object?)"Failed")]);
    }

    [Fact]
    public async Task Sweep_ListingFails_RecordsFailedRun()
    {
        using var runs = new MetricCollector<long>(_meterFactory, ElmanhgTelemetry.SourceName, "elmanhg.job.runs");
        _sender.Send(Arg.Any<GetDueVoiceDraftIdsQuery>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            Complete();
            return Task.FromException<List<Guid>>(new InvalidOperationException());
        });

        using var worker = await RunAsync();
        await runs.WaitForMeasurementsAsync(1, WaitLimit);

        runs.LastMeasurement!.Tags.Should().Contain(BackgroundJobMetrics.JobTag, "teacher-voice-transcription").And.Contain(BackgroundJobMetrics.OutcomeTag, "Failed");
    }

    private async Task<TeacherVoiceTranscriptionWorker> RunAsync()
    {
        var worker = new TeacherVoiceTranscriptionWorker(ScopeFactory(), Options.Create(new AskTeacherOptions()), _time, _logger, new BackgroundJobMetrics(_meterFactory, _time));
        await worker.StartAsync(TestContext.Current.CancellationToken);
        await _time.TimerCreated.WaitAsync(WaitLimit, TestContext.Current.CancellationToken);
        _time.Tick();
        await Task.WhenAny(_done.Task, worker.ExecuteTask!).WaitAsync(WaitLimit, TestContext.Current.CancellationToken);
        _done.Task.IsCompleted.Should().BeTrue();
        return worker;
    }

    private IServiceScopeFactory ScopeFactory() => new ServiceCollection().AddSingleton(_sender).BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

    private void CompleteOnTranscribe(Guid draftId) => _sender.Send(Arg.Is<TranscribeVoiceDraftCommand>(x => x.DraftId == draftId), Arg.Any<CancellationToken>()).Returns(_ =>
    {
        Complete();
        return Task.CompletedTask;
    });

    private void Complete() => _done.TrySetResult();

    private List<LogLevel> LoggedLevels() => _logger.ReceivedCalls().Where(x => x.GetMethodInfo().Name == nameof(ILogger.Log)).Select(x => (LogLevel)x.GetArguments()[0]!).ToList();
}
