using Core.OTP.Delivery;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;

namespace Elmanhg.Infrastructure.OtpDelivery.Sms;

public sealed class HttpSmsOtpChannel(HttpClient httpClient, IOptions<OtpDeliveryOptions> otpDeliveryOptions, ILogger<HttpSmsOtpChannel> logger) : IOtpChannel
{
    public const string CodeToken = "{code}";

    public OtpChannel Channel => OtpChannel.Sms;

    public async Task SendAsync(string recipient, string code, CancellationToken cancellationToken)
    {
        var options = otpDeliveryOptions.Value;
        var sms = options.Sms;
        var message = sms.MessageTemplate.Replace(CodeToken, code, StringComparison.Ordinal);
        var body = HttpSmsBodyRenderer.Render(sms.BodyTemplate, sms.ContentType, recipient, PhoneNumberFormatter.ToInternational(recipient, options.CountryCallingCode), message);
        using var request = new HttpRequestMessage(HttpMethod.Post, sms.Url)
        {
            Content = new StringContent(body, Encoding.UTF8, sms.ContentType),
        };
        if (!string.IsNullOrWhiteSpace(sms.AuthHeaderName))
        {
            request.Headers.TryAddWithoutValidation(sms.AuthHeaderName, sms.AuthHeaderValue);
        }

        await httpClient.SendOtpRequestAsync(request, Channel, logger, cancellationToken).ConfigureAwait(false);
    }
}
