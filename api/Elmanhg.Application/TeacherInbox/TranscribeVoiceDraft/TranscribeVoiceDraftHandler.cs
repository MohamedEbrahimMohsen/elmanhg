using Core.Errors;
using Core.Storage;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.TeacherThreads;
using MediatR;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.TeacherInbox.TranscribeVoiceDraft;

public sealed class TranscribeVoiceDraftHandler(ITeacherVoiceDraftRepository teacherVoiceDraftRepository, IFileStorage fileStorage, IAiTranscriptionClient transcriptionClient, IOptions<AskTeacherOptions> askTeacherOptions, TimeProvider timeProvider) : IRequestHandler<TranscribeVoiceDraftCommand>
{
    public async Task Handle(TranscribeVoiceDraftCommand request, CancellationToken cancellationToken)
    {
        var draft = await teacherVoiceDraftRepository.FirstOrDefaultAsync(x => x.Id == request.DraftId, cancellationToken).ConfigureAwait(false);
        if (draft is null || !draft.IsDueAt(timeProvider.GetUtcNow()))
        {
            return;
        }

        await using var audio = await fileStorage.OpenReadAsync(draft.AudioKey, cancellationToken).ConfigureAwait(false) ?? throw new NotFoundCoreException(ErrorCodes.TeacherVoiceAudioNotFound);
        using var buffer = new MemoryStream();
        await audio.Content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);

        var result = await transcriptionClient.TranscribeAsync(new AiTranscriptionRequest(buffer.ToArray(), audio.ContentType, askTeacherOptions.Value.TranscriptionLanguage, draft.AudioDurationSeconds), cancellationToken).ConfigureAwait(false);
        draft.CompleteTranscription(result.Text, result.Model, timeProvider.GetUtcNow());

        await teacherVoiceDraftRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
