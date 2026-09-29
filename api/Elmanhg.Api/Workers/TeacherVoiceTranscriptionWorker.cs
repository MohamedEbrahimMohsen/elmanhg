using Elmanhg.Application.Shared.Observability;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.TeacherInbox.FailVoiceDraftTranscription;
using Elmanhg.Application.TeacherInbox.GetDueVoiceDraftIds;
using Elmanhg.Application.TeacherInbox.TranscribeVoiceDraft;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Api.Workers;

public sealed class TeacherVoiceTranscriptionWorker(IServiceScopeFactory scopeFactory, IOptions<AskTeacherOptions> askTeacherOptions, TimeProvider timeProvider, ILogger<TeacherVoiceTranscriptionWorker> logger, BackgroundJobMetrics jobMetrics) : BackgroundService
{
    private const string JobName = "teacher-voice-transcription";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = askTeacherOptions.Value;
        if (!options.TranscriptionSweepEnabled)
        {
            return;
        }

        jobMetrics.Register(JobName, TimeSpan.FromSeconds(options.TranscriptionSweepIntervalSeconds));
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.TranscriptionSweepIntervalSeconds), timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            await SweepAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task SweepAsync(CancellationToken stoppingToken)
    {
        using var run = jobMetrics.StartRun(JobName);
        var draftIds = await ListDueAsync(stoppingToken).ConfigureAwait(false);
        if (draftIds is null)
        {
            run.MarkListingFailed();
            return;
        }

        foreach (var draftId in draftIds)
        {
            if (await TranscribeAsync(draftId, stoppingToken).ConfigureAwait(false))
            {
                run.ItemSucceeded();
                continue;
            }

            run.ItemFailed();
        }
    }

    private async Task<List<Guid>?> ListDueAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new GetDueVoiceDraftIdsQuery(), stoppingToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Listing due voice drafts failed.");
            return null;
        }
    }

    private async Task<bool> TranscribeAsync(Guid draftId, CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new TranscribeVoiceDraftCommand(draftId), stoppingToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Transcription of voice draft {DraftId} failed.", draftId);
            await RecordFailureAsync(draftId, stoppingToken).ConfigureAwait(false);
            return false;
        }
    }

    private async Task RecordFailureAsync(Guid draftId, CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ISender>().Send(new FailVoiceDraftTranscriptionCommand(draftId), stoppingToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Recording the failed transcription of voice draft {DraftId} failed.", draftId);
        }
    }
}
