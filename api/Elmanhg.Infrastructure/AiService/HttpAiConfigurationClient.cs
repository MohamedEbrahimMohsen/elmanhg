using Core.Http;
using Elmanhg.Application.Configuration.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Elmanhg.Infrastructure.AiService;

public sealed class HttpAiConfigurationClient(HttpClient httpClient, IOptions<AiServiceOptions> aiServiceOptions, IOptions<CoreHttpOptions> coreHttpOptions, ILogger<HttpAiConfigurationClient> logger) : AiServiceHttpClient(httpClient, aiServiceOptions, coreHttpOptions, logger)
{
    private const string ConfigurationPath = "v1/configuration";

    public async Task<AiServiceConfigurationResult?> GetAsync(CancellationToken cancellationToken)
    {
        var reply = await TryGetAsync<AiServiceConfigurationResult>(ConfigurationPath, cancellationToken).ConfigureAwait(false);
        if (!reply.Succeeded)
        {
            Logger.LogWarning(reply.Exception, "AI service configuration call failed: {Failure}, HTTP {StatusCode}.", reply.Failure, reply.StatusCode);
            return null;
        }

        return reply.Value;
    }
}
