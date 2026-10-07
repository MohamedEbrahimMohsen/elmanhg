using Core.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace Elmanhg.Infrastructure.AiService;

public abstract class AiServiceHttpClient(HttpClient httpClient, IOptions<AiServiceOptions> aiServiceOptions, IOptions<CoreHttpOptions> coreHttpOptions, ILogger logger)
{
    private const string BearerScheme = "Bearer";

    protected ILogger Logger => logger;

    protected Task<TResponse?> PostAsync<TRequest, TResponse>(string path, TRequest request, string errorCode, CancellationToken cancellationToken) => httpClient.SendJsonAsync<TResponse>(HttpJsonCall.Post(path, request, AiJsonSerializerOptions.Default, Authorization(), coreHttpOptions.Value.UserAgent), AiJsonSerializerOptions.Default, errorCode, logger, cancellationToken);

    protected Task<HttpJsonReply<TResponse>> TryPostAsync<TRequest, TResponse>(string path, TRequest request, CancellationToken cancellationToken) => httpClient.TrySendJsonAsync<TResponse>(HttpJsonCall.Post(path, request, AiJsonSerializerOptions.Default, Authorization(), coreHttpOptions.Value.UserAgent), AiJsonSerializerOptions.Default, cancellationToken);

    protected Task<HttpJsonReply<TResponse>> TryGetAsync<TResponse>(string path, CancellationToken cancellationToken) => httpClient.TrySendJsonAsync<TResponse>(HttpJsonCall.Get(path, Authorization(), coreHttpOptions.Value.UserAgent), AiJsonSerializerOptions.Default, cancellationToken);

    private AuthenticationHeaderValue Authorization() => new(BearerScheme, aiServiceOptions.Value.ServiceToken);
}
