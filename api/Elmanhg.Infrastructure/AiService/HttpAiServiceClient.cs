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
    private const string BearerScheme = "Bearer";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public async Task<AiChatReply> ChatAsync(AiChatRequest request, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, ChatPath)
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
            logger.LogError(exception, "AI service chat call failed before a response arrived.");
            throw new ServiceUnavailableCoreException(ErrorCodes.AiServiceUnavailable, innerException: exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("AI service rejected the chat call with HTTP {StatusCode}.", (int)response.StatusCode);
                throw new ServiceUnavailableCoreException(ErrorCodes.AiServiceUnavailable);
            }

            return await ReadReplyAsync(response, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<AiChatReply> ReadReplyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        AiChatReply? reply;
        try
        {
            reply = await response.Content.ReadFromJsonAsync<AiChatReply>(SerializerOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "AI service returned an unreadable chat reply.");
            throw new ServiceUnavailableCoreException(ErrorCodes.AiServiceUnavailable, innerException: exception);
        }

        if (string.IsNullOrWhiteSpace(reply?.Reply))
        {
            logger.LogError("AI service returned an empty chat reply.");
            throw new ServiceUnavailableCoreException(ErrorCodes.AiServiceUnavailable);
        }

        return reply;
    }
}
