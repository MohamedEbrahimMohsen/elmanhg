using Core.Queues;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.TeacherInbox.FailVoiceDraftTranscription;
using Elmanhg.Application.TeacherInbox.GetDueVoiceDraftIds;
using Elmanhg.Application.TeacherInbox.TranscribeVoiceDraft;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Api.Workers;

public sealed class TeacherVoiceTranscriptionWorker(IServiceScopeFactory scopeFactory, IOptions<AskTeacherOptions> askTeacherOptions, TimeProvider timeProvider, ILogger<TeacherVoiceTranscriptionWorker> logger, BackgroundJobMetrics jobMetrics) : SweepWorker<AskTeacherOptions>(scopeFactory, askTeacherOptions, timeProvider, logger, jobMetrics)
{
    protected override string JobName => "teacher-voice-transcription";

    protected override SweepOptions SweepOptionsOf(AskTeacherOptions options) => new() { Enabled = options.TranscriptionSweepEnabled, IntervalSeconds = options.TranscriptionSweepIntervalSeconds, BatchSize = options.TranscriptionSweepBatchSize };

    protected override async Task<List<Guid>> ListDueAsync(ISender sender, IReadOnlyCollection<Guid> deferredIds, CancellationToken cancellationToken) => await sender.Send(new GetDueVoiceDraftIdsQuery(), cancellationToken).ConfigureAwait(false);

    protected override async Task ProcessAsync(ISender sender, Guid id, CancellationToken cancellationToken) => await sender.Send(new TranscribeVoiceDraftCommand(id), cancellationToken).ConfigureAwait(false);

    protected override async Task RecordFailureAsync(ISender sender, Guid id, string errorCode, CancellationToken cancellationToken) => await sender.Send(new FailVoiceDraftTranscriptionCommand(id), cancellationToken).ConfigureAwait(false);
}
