using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.TeacherThreads;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.TeacherInbox.FailVoiceDraftTranscription;

public sealed class FailVoiceDraftTranscriptionHandler(ITeacherVoiceDraftRepository teacherVoiceDraftRepository, IOptions<AskTeacherOptions> askTeacherOptions, TimeProvider timeProvider) : IRequestHandler<FailVoiceDraftTranscriptionCommand>
{
    public async Task Handle(FailVoiceDraftTranscriptionCommand request, CancellationToken cancellationToken)
    {
        var draft = await teacherVoiceDraftRepository.FirstOrDefaultAsync(x => x.Id == request.DraftId, cancellationToken).ConfigureAwait(false);
        if (draft is null || draft.Status != TeacherVoiceDraftStatus.Pending)
        {
            return;
        }

        var options = askTeacherOptions.Value;
        draft.FailAttempt(timeProvider.GetUtcNow(), options.TranscriptionMaxAttempts, TimeSpan.FromSeconds(options.TranscriptionRetryBaseDelaySeconds));

        await teacherVoiceDraftRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
