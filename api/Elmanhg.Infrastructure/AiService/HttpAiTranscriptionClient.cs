using Core.Errors;
using Core.Http;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.AiService;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Elmanhg.Infrastructure.AiService;

public sealed class HttpAiTranscriptionClient(HttpClient httpClient, IOptions<AiServiceOptions> aiServiceOptions, IOptions<CoreHttpOptions> coreHttpOptions, ILogger<HttpAiTranscriptionClient> logger) : AiServiceHttpClient(httpClient, aiServiceOptions, coreHttpOptions, logger), IAiTranscriptionClient
{
    private const string TranscriptionsPath = "v1/transcriptions";

    public async Task<AiTranscriptionResult> TranscribeAsync(AiTranscriptionRequest request, CancellationToken cancellationToken)
    {
        var result = await PostAsync<AiTranscriptionRequest, AiTranscriptionResult>(TranscriptionsPath, request, ErrorCodes.AiServiceUnavailable, cancellationToken).ConfigureAwait(false);
        if (result?.Text is null || string.IsNullOrWhiteSpace(result.Model))
        {
            Logger.LogError("AI service returned an invalid transcription reply.");
            throw new ServiceUnavailableCoreException(ErrorCodes.AiServiceUnavailable);
        }

        return result;
    }
}
