using Core.Messaging;
using Core.Messaging.Sms;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Core.OTP.Delivery.Sms;

public sealed class HttpSmsOtpChannel(HttpSmsClient httpSmsClient, IOptions<OtpDeliveryOptions> otpDeliveryOptions, OtpDeliverySetup setup, ILogger<HttpSmsOtpChannel> logger) : IOtpChannel
{
    public const string CodeToken = "{code}";

    public OtpChannel Channel => OtpChannel.Sms;

    public async Task SendAsync(string recipient, string code, CancellationToken cancellationToken)
    {
        var options = otpDeliveryOptions.Value;
        var sms = options.Sms;
        var message = sms.MessageTemplate.Replace(CodeToken, code, StringComparison.Ordinal);
        var result = await httpSmsClient.SendAsync(new HttpSmsGateway(sms.Url, sms.ContentType, sms.BodyTemplate, sms.AuthHeaderName, sms.AuthHeaderValue), recipient, PhoneNumberFormatter.ToInternational(recipient, options.CountryCallingCode), message, cancellationToken).ConfigureAwait(false);
        result.EnsureDelivered(Channel, setup.DeliveryFailedErrorCode, logger);
    }
}
