using Core.Errors;
using Core.Http;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.AiService;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Elmanhg.Infrastructure.AiService;

public sealed class HttpAiMathStepGradingClient(HttpClient httpClient, IOptions<AiServiceOptions> aiServiceOptions, IOptions<CoreHttpOptions> coreHttpOptions, ILogger<HttpAiMathStepGradingClient> logger) : AiServiceHttpClient(httpClient, aiServiceOptions, coreHttpOptions, logger), IAiMathStepGradingClient
{
    private const string MathStepGradesPath = "v1/math-step-grades";

    public async Task<AiMathStepGradingResult> GradeAsync(AiMathStepGradingRequest request, CancellationToken cancellationToken)
    {
        var result = await PostAsync<AiMathStepGradingRequest, AiMathStepGradingResult>(MathStepGradesPath, request, ErrorCodes.MathStepGradingUnavailable, cancellationToken).ConfigureAwait(false);
        if (!AiMathStepGradingReplyRules.IsValid(request, result))
        {
            Logger.LogError("AI service returned an invalid math step grading reply.");
            throw new ServiceUnavailableCoreException(ErrorCodes.MathStepGradingUnavailable);
        }

        return result!;
    }
}
