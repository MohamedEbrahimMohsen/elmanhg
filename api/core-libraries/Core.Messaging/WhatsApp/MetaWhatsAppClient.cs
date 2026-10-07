using Core.Http;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;

namespace Core.Messaging.WhatsApp;

public sealed class MetaWhatsAppClient(HttpClient httpClient, IOptions<CoreHttpOptions> coreHttpOptions)
{
    private const string BearerScheme = "Bearer";

    public async Task<HttpSendResult> SendTemplateAsync(MetaWhatsAppTemplateMessage message, MetaWhatsAppSender sender, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{sender.ApiVersion}/{sender.PhoneNumberId}/messages")
        {
            Content = JsonContent.Create(message),
        };
        request.Headers.Authorization = new(BearerScheme, sender.AccessToken);
        request.WithUserAgent(coreHttpOptions.Value.UserAgent);
        return await httpClient.TrySendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
