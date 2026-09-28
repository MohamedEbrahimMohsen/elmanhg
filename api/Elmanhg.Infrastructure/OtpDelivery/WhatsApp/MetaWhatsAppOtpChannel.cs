using Core.OTP.Delivery;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Elmanhg.Infrastructure.OtpDelivery.WhatsApp;

public sealed class MetaWhatsAppOtpChannel(HttpClient httpClient, IOptions<OtpDeliveryOptions> otpDeliveryOptions, ILogger<MetaWhatsAppOtpChannel> logger) : IOtpChannel
{
    private const string BearerScheme = "Bearer";

    public OtpChannel Channel => OtpChannel.WhatsApp;

    public async Task SendAsync(string recipient, string code, CancellationToken cancellationToken)
    {
        var options = otpDeliveryOptions.Value;
        var whatsApp = options.WhatsApp;
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{whatsApp.ApiVersion}/{whatsApp.PhoneNumberId}/messages")
        {
            Content = JsonContent.Create(MetaWhatsAppTemplateMessage.Create(PhoneNumberFormatter.ToInternational(recipient, options.CountryCallingCode), code, whatsApp)),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, whatsApp.AccessToken);
        await httpClient.SendOtpRequestAsync(request, Channel, logger, cancellationToken).ConfigureAwait(false);
    }
}
