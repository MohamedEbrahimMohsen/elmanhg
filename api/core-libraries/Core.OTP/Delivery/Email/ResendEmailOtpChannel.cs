using Core.Messaging.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Core.OTP.Delivery.Email;

public sealed class ResendEmailOtpChannel(ResendEmailClient resendEmailClient, IOptions<OtpDeliveryOptions> otpDeliveryOptions, IOptions<OtpOptions> otpOptions, OtpDeliverySetup setup, ILogger<ResendEmailOtpChannel> logger) : IOtpChannel
{
    public OtpChannel Channel => OtpChannel.Email;

    public async Task SendAsync(string recipient, string code, CancellationToken cancellationToken)
    {
        var email = otpDeliveryOptions.Value.Email;
        var content = setup.RenderEmail(code, otpOptions.Value.ExpirationMinutes);
        var result = await resendEmailClient.SendAsync(new EmailMessage(email.FromAddress, recipient, email.Subject, content.Html, content.Text), email.ApiKey, Guid.NewGuid().ToString("N"), cancellationToken).ConfigureAwait(false);
        result.EnsureDelivered(Channel, setup.DeliveryFailedErrorCode, logger);
    }
}
