using Elmanhg.Application.Configuration.Shared;
using Elmanhg.Infrastructure.OtpDelivery;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Elmanhg.Infrastructure.AiService;

public sealed class HttpAiConfigurationClient(HttpClient httpClient, IOptions<AiServiceOptions> aiServiceOptions, ILogger<HttpAiConfigurationClient> logger)
{
    private const string ConfigurationPath = "v1/configuration";
    private const string BearerScheme = "Bearer";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<AiServiceConfigurationResult?> GetAsync(CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, ConfigurationPath);
        message.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, aiServiceOptions.Value.ServiceToken);
        message.Headers.UserAgent.ParseAdd(OtpProviderHttpExtensions.UserAgent);
        try
        {
            using var response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("AI service configuration call returned {StatusCode}.", (int)response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<AiServiceConfigurationResult>(SerializerOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException or ExecutionRejectedException or JsonException || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogWarning(exception, "AI service configuration call failed.");
            return null;
        }
    }
}
