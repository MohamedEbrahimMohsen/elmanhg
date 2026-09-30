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

public sealed class HttpAiEssayGradingClient(HttpClient httpClient, IOptions<AiServiceOptions> aiServiceOptions, ILogger<HttpAiEssayGradingClient> logger) : IAiEssayGradingClient
{
    private const string EssayGradesPath = "v1/essay-grades";
    private const string BearerScheme = "Bearer";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public async Task<AiEssayGradingResult> GradeAsync(AiEssayGradingRequest request, CancellationToken cancellationToken)
    {
        var result = await PostAsync(request, cancellationToken).ConfigureAwait(false);
        if (!AiEssayGradingReplyRules.IsValid(request, result))
        {
            logger.LogError("AI service returned an invalid essay grading reply.");
            throw new ServiceUnavailableCoreException(ErrorCodes.EssayGradingUnavailable);
        }

        return result!;
    }

    private async Task<AiEssayGradingResult?> PostAsync(AiEssayGradingRequest request, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, EssayGradesPath)
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
            logger.LogError(exception, "AI service essay grading call failed before a response arrived.");
            throw new ServiceUnavailableCoreException(ErrorCodes.EssayGradingUnavailable, innerException: exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("AI service rejected the essay grading call with HTTP {StatusCode}.", (int)response.StatusCode);
                throw new ServiceUnavailableCoreException(ErrorCodes.EssayGradingUnavailable);
            }

            try
            {
                return await response.Content.ReadFromJsonAsync<AiEssayGradingResult>(SerializerOptions, cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException exception)
            {
                logger.LogError(exception, "AI service returned an unreadable essay grading reply.");
                throw new ServiceUnavailableCoreException(ErrorCodes.EssayGradingUnavailable, innerException: exception);
            }
        }
    }
}
