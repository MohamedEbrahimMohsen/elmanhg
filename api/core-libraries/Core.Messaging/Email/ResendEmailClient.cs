using Core.Http;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;

namespace Core.Messaging.Email;

public sealed class ResendEmailClient(HttpClient httpClient, IOptions<CoreHttpOptions> coreHttpOptions)
{
    private const string EmailsPath = "emails";
    private const string IdempotencyKeyHeader = "Idempotency-Key";
    private const string BearerScheme = "Bearer";

    public async Task<HttpSendResult> SendAsync(EmailMessage message, string apiKey, string idempotencyKey, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, EmailsPath)
        {
            Content = JsonContent.Create(ResendEmailRequest.Create(message)),
        };
        request.Headers.Authorization = new(BearerScheme, apiKey);
        request.Headers.Add(IdempotencyKeyHeader, idempotencyKey);
        request.WithUserAgent(coreHttpOptions.Value.UserAgent);
        return await httpClient.TrySendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
