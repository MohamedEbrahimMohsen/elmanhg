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

public sealed class HttpAiTranscriptionClient(HttpClient httpClient, IOptions<AiServiceOptions> aiServiceOptions, ILogger<HttpAiTranscriptionClient> logger) : IAiTranscriptionClient
{
    private const string TranscriptionsPath = "v1/transcriptions";
    private const string BearerScheme = "Bearer";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public async Task<AiTranscriptionResult> TranscribeAsync(AiTranscriptionRequest request, CancellationToken cancellationToken)
    {
        var result = await PostAsync(request, cancellationToken).ConfigureAwait(false);
        if (result?.Text is null || string.IsNullOrWhiteSpace(result.Model))
        {
            logger.LogError("AI service returned an invalid transcription reply.");
            throw new ServiceUnavailableCoreException(ErrorCodes.AiServiceUnavailable);
        }

        return result;
    }

    private async Task<AiTranscriptionResult?> PostAsync(AiTranscriptionRequest request, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, TranscriptionsPath)
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
            logger.LogError(exception, "AI service transcription call failed before a response arrived.");
            throw new ServiceUnavailableCoreException(ErrorCodes.AiServiceUnavailable, innerException: exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("AI service rejected the transcription call with HTTP {StatusCode}.", (int)response.StatusCode);
                throw new ServiceUnavailableCoreException(ErrorCodes.AiServiceUnavailable);
            }

            try
            {
                return await response.Content.ReadFromJsonAsync<AiTranscriptionResult>(SerializerOptions, cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException exception)
            {
                logger.LogError(exception, "AI service returned an unreadable transcription reply.");
                throw new ServiceUnavailableCoreException(ErrorCodes.AiServiceUnavailable, innerException: exception);
            }
        }
    }
}
