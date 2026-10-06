using Core.Http;
using Microsoft.Extensions.Options;
using System.Text;

namespace Core.Messaging.Sms;

public sealed class HttpSmsClient(HttpClient httpClient, IOptions<CoreHttpOptions> coreHttpOptions)
{
    public async Task<HttpSendResult> SendAsync(HttpSmsGateway gateway, string phoneNumber, string internationalPhoneNumber, string message, CancellationToken cancellationToken)
    {
        var body = HttpSmsBodyRenderer.Render(gateway.BodyTemplate, gateway.ContentType, phoneNumber, internationalPhoneNumber, message);
        using var request = new HttpRequestMessage(HttpMethod.Post, gateway.Url)
        {
            Content = new StringContent(body, Encoding.UTF8, gateway.ContentType),
        };
        if (!string.IsNullOrWhiteSpace(gateway.AuthHeaderName))
        {
            request.Headers.TryAddWithoutValidation(gateway.AuthHeaderName, gateway.AuthHeaderValue);
        }

        request.WithUserAgent(coreHttpOptions.Value.UserAgent);
        return await httpClient.TrySendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
