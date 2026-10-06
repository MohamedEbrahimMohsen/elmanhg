using Core.Messaging;
using Core.Messaging.WhatsApp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Core.OTP.Delivery.WhatsApp;

public sealed class MetaWhatsAppOtpChannel(MetaWhatsAppClient metaWhatsAppClient, IOptions<OtpDeliveryOptions> otpDeliveryOptions, OtpDeliverySetup setup, ILogger<MetaWhatsAppOtpChannel> logger) : IOtpChannel
{
    public OtpChannel Channel => OtpChannel.WhatsApp;

    public async Task SendAsync(string recipient, string code, CancellationToken cancellationToken)
    {
        var options = otpDeliveryOptions.Value;
        var whatsApp = options.WhatsApp;
        var message = MetaWhatsAppTemplateMessage.Create(PhoneNumberFormatter.ToInternational(recipient, options.CountryCallingCode), whatsApp.TemplateName, whatsApp.LanguageCode, [code], whatsApp.CopyCodeButton ? code : null);
        var result = await metaWhatsAppClient.SendTemplateAsync(message, new MetaWhatsAppSender(whatsApp.ApiVersion, whatsApp.PhoneNumberId, whatsApp.AccessToken), cancellationToken).ConfigureAwait(false);
        result.EnsureDelivered(Channel, setup.DeliveryFailedErrorCode, logger);
    }
}
