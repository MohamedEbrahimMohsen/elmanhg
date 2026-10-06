using Core.Errors;
using Core.Http;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.AiService;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Elmanhg.Infrastructure.AiService;

public sealed class HttpAiServiceClient(HttpClient httpClient, IOptions<AiServiceOptions> aiServiceOptions, IOptions<CoreHttpOptions> coreHttpOptions, ILogger<HttpAiServiceClient> logger) : AiServiceHttpClient(httpClient, aiServiceOptions, coreHttpOptions, logger), IAiServiceClient
{
    private const string ChatPath = "v1/chat";
    private const string EmbeddingsPath = "v1/embeddings";

    public async Task<AiChatReply> ChatAsync(AiChatRequest request, CancellationToken cancellationToken)
    {
        var reply = await PostAsync<AiChatRequest, AiChatReply>(ChatPath, request, ErrorCodes.AiServiceUnavailable, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(reply?.Reply))
        {
            Logger.LogError("AI service returned an empty chat reply.");
            throw new ServiceUnavailableCoreException(ErrorCodes.AiServiceUnavailable);
        }

        return reply.Citations is null ? reply with { Citations = [] } : reply;
    }

    public async Task<AiEmbeddingResult> EmbedAsync(AiEmbeddingRequest request, CancellationToken cancellationToken)
    {
        var result = await PostAsync<AiEmbeddingRequest, AiEmbeddingResult>(EmbeddingsPath, request, ErrorCodes.AiServiceUnavailable, cancellationToken).ConfigureAwait(false);
        if (result is null || string.IsNullOrWhiteSpace(result.Model) || result.Dimensions <= 0 || result.Embeddings is null || result.Embeddings.Count != request.Texts.Count || result.Embeddings.Any(x => x is null || x.Length != result.Dimensions))
        {
            Logger.LogError("AI service returned an invalid embeddings reply.");
            throw new ServiceUnavailableCoreException(ErrorCodes.AiServiceUnavailable);
        }

        return result;
    }
}
