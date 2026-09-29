namespace Elmanhg.Application.Shared.AiService;

public interface IAiTranscriptionClient
{
    Task<AiTranscriptionResult> TranscribeAsync(AiTranscriptionRequest request, CancellationToken cancellationToken);
}
