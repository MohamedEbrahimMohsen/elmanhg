using Core.Errors;
using Core.Http;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.AiService;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Elmanhg.Infrastructure.AiService;

public sealed class HttpAiEssayGradingClient(HttpClient httpClient, IOptions<AiServiceOptions> aiServiceOptions, IOptions<CoreHttpOptions> coreHttpOptions, ILogger<HttpAiEssayGradingClient> logger) : AiServiceHttpClient(httpClient, aiServiceOptions, coreHttpOptions, logger), IAiEssayGradingClient
{
    private const string EssayGradesPath = "v1/essay-grades";

    public async Task<AiEssayGradingResult> GradeAsync(AiEssayGradingRequest request, CancellationToken cancellationToken)
    {
        var result = await PostAsync<AiEssayGradingRequest, AiEssayGradingResult>(EssayGradesPath, request, ErrorCodes.EssayGradingUnavailable, cancellationToken).ConfigureAwait(false);
        if (!AiEssayGradingReplyRules.IsValid(request, result))
        {
            Logger.LogError("AI service returned an invalid essay grading reply.");
            throw new ServiceUnavailableCoreException(ErrorCodes.EssayGradingUnavailable);
        }

        return result!;
    }
}
