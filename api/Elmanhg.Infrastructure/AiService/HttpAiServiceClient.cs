using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Infrastructure.OtpDelivery;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Elmanhg.Infrastructure.AiService;

public sealed class HttpAiServiceClient(HttpClient httpClient, IOptions<AiServiceOptions> aiServiceOptions, ILogger<HttpAiServiceClient> logger) : IAiServiceClient
{
    private const string ChatPath = "v1/chat";
    private const string EmbeddingsPath = "v1/embeddings";
    private const string BearerScheme = "Bearer";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public async Task<AiChatReply> ChatAsync(AiChatRequest request, CancellationToken cancellationToken)
    {
        var reply = await PostAsync<AiChatRequest, AiChatReply>(ChatPath, request, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(reply?.Reply))
        {
            logger.LogError("AI service returned an empty chat reply.");
            throw new ServiceUnavailableCoreException(ErrorCodes.AiServiceUnavailable);
        }

        return reply.Citations is null ? reply with { Citations = [] } : reply;
    }

    public async Task<AiEmbeddingResult> EmbedAsync(AiEmbeddingRequest request, CancellationToken cancellationToken)
    {
        var result = await PostAsync<AiEmbeddingRequest, AiEmbeddingResult>(EmbeddingsPath, request, cancellationToken).ConfigureAwait(false);
        if (result is null || string.IsNullOrWhiteSpace(result.Model) || result.Dimensions <= 0 || result.Embeddings is null || result.Embeddings.Count != request.Texts.Count || result.Embeddings.Any(x => x is null || x.Length != result.Dimensions))
        {
            logger.LogError("AI service returned an invalid embeddings reply.");
            throw new ServiceUnavailableCoreException(ErrorCodes.AiServiceUnavailable);
        }

        return result;
    }

    private async Task<TResponse?> PostAsync<TRequest, TResponse>(string path, TRequest request, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(request, options: SerializerOptions),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, aiServiceOptions.Value.ServiceToken);
        message.Headers.UserAgent.ParseAdd(OtpProviderHttpExtensions.UserAgent);
        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException or ExecutionRejectedException || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogError(exception, "AI service call to {Path} failed before a response arrived.", path);
            throw new ServiceUnavailableCoreException(ErrorCodes.AiServiceUnavailable, innerException: exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("AI service rejected the call to {Path} with HTTP {StatusCode}.", path, (int)response.StatusCode);
                throw new ServiceUnavailableCoreException(ErrorCodes.AiServiceUnavailable);
            }

            try
            {
                return await response.Content.ReadFromJsonAsync<TResponse>(SerializerOptions, cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException exception)
            {
                logger.LogError(exception, "AI service returned an unreadable reply from {Path}.", path);
                throw new ServiceUnavailableCoreException(ErrorCodes.AiServiceUnavailable, innerException: exception);
            }
        }
    }
}
