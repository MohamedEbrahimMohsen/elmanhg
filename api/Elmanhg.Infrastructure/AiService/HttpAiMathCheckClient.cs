using Core.Http;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Domain.Questions.Grading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Elmanhg.Infrastructure.AiService;

public sealed class HttpAiMathCheckClient(HttpClient httpClient, IOptions<AiServiceOptions> aiServiceOptions, IOptions<CoreHttpOptions> coreHttpOptions, ILogger<HttpAiMathCheckClient> logger) : AiServiceHttpClient(httpClient, aiServiceOptions, coreHttpOptions, logger), IAiMathCheckClient
{
    private const string MathChecksPath = "v1/math-checks";

    private static readonly AiMathCheckResult Unchecked = new(MathAnswerVerdict.Unchecked, null, []);

    public async Task<AiMathCheckResult> CheckAsync(AiMathCheckRequest request, CancellationToken cancellationToken)
    {
        var reply = await TryPostAsync<AiMathCheckRequest, MathCheckReply>(MathChecksPath, request, cancellationToken).ConfigureAwait(false);
        if (!reply.Succeeded)
        {
            Logger.LogError(reply.Exception, "AI service math check call failed: {Failure}, HTTP {StatusCode}.", reply.Failure, reply.StatusCode);
            return Unchecked;
        }

        var value = reply.Value ?? new MathCheckReply(null, null, null);
        if (value.Verdict is not { } verdict || !Enum.IsDefined(verdict) || value.InvalidExpected is null)
        {
            Logger.LogError("AI service returned an invalid math check reply.");
            return Unchecked;
        }

        if (verdict == MathAnswerVerdict.Unchecked || value.InvalidExpected.Count > 0)
        {
            Logger.LogWarning("Math check verdict {Verdict} with {InvalidCount} unreadable accepted answers.", verdict, value.InvalidExpected.Count);
        }

        return new AiMathCheckResult(verdict, value.MatchedIndex, value.InvalidExpected);
    }

    private sealed record MathCheckReply(MathAnswerVerdict? Verdict, int? MatchedIndex, List<int>? InvalidExpected);
}
