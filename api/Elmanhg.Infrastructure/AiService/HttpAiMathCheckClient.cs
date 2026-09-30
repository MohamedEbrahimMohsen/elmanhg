using Elmanhg.Application.Shared.AiService;
using Elmanhg.Domain.Questions.Grading;
using Elmanhg.Infrastructure.OtpDelivery;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Elmanhg.Infrastructure.AiService;

public sealed class HttpAiMathCheckClient(HttpClient httpClient, IOptions<AiServiceOptions> aiServiceOptions, ILogger<HttpAiMathCheckClient> logger) : IAiMathCheckClient
{
    private const string MathChecksPath = "v1/math-checks";
    private const string BearerScheme = "Bearer";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private static readonly AiMathCheckResult Unchecked = new(MathAnswerVerdict.Unchecked, null, []);

    public async Task<AiMathCheckResult> CheckAsync(AiMathCheckRequest request, CancellationToken cancellationToken)
    {
        var reply = await PostAsync(request, cancellationToken).ConfigureAwait(false);
        if (reply is null)
        {
            return Unchecked;
        }

        if (reply.Verdict is not { } verdict || !Enum.IsDefined(verdict) || reply.InvalidExpected is null)
        {
            logger.LogError("AI service returned an invalid math check reply.");
            return Unchecked;
        }

        if (verdict == MathAnswerVerdict.Unchecked || reply.InvalidExpected.Count > 0)
        {
            logger.LogWarning("Math check verdict {Verdict} with {InvalidCount} unreadable accepted answers.", verdict, reply.InvalidExpected.Count);
        }

        return new AiMathCheckResult(verdict, reply.MatchedIndex, reply.InvalidExpected);
    }

    private async Task<MathCheckReply?> PostAsync(AiMathCheckRequest request, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, MathChecksPath)
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
            logger.LogError(exception, "AI service math check call failed before a response arrived.");
            return null;
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("AI service rejected the math check call with HTTP {StatusCode}.", (int)response.StatusCode);
                return null;
            }

            try
            {
                return await response.Content.ReadFromJsonAsync<MathCheckReply>(SerializerOptions, cancellationToken).ConfigureAwait(false) ?? new MathCheckReply(null, null, null);
            }
            catch (JsonException exception)
            {
                logger.LogError(exception, "AI service returned an unreadable math check reply.");
                return null;
            }
        }
    }

    private sealed record MathCheckReply(MathAnswerVerdict? Verdict, int? MatchedIndex, List<int>? InvalidExpected);
}
