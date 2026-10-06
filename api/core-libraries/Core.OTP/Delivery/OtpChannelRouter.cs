using Core.Errors;
using Core.OTP.Entities;
using Microsoft.Extensions.Options;

namespace Core.OTP.Delivery;

public sealed class OtpChannelRouter(IOptions<OtpDeliveryOptions> otpDeliveryOptions, IEnumerable<IOtpChannel> channels, IEnumerable<IOtpDeliveryObserver> observers, OtpDeliverySetup setup) : IOtpSender
{
    private readonly OtpDeliveryOptions _options = otpDeliveryOptions.Value;

    public async Task<OtpChannel> SendAsync(OtpRecipientType recipientType, string recipient, string code, CancellationToken cancellationToken)
    {
        var channel = Resolve(recipientType);
        try
        {
            await channels.Single(x => x.Channel == channel).SendAsync(recipient, code, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            Notify(channel, delivered: false);
            throw;
        }

        Notify(channel, delivered: true);
        return channel;
    }

    private void Notify(OtpChannel channel, bool delivered)
    {
        foreach (var observer in observers)
        {
            observer.RecordOtpSend(channel, delivered);
        }
    }

    private OtpChannel Resolve(OtpRecipientType recipientType)
    {
        if (recipientType == OtpRecipientType.Email)
        {
            return IsEnabled(OtpChannel.Email) ? OtpChannel.Email : throw new ServiceUnavailableCoreException(setup.ChannelUnavailableErrorCode);
        }

        var preferred = _options.DefaultPhoneChannel.GetValueOrDefault();
        var fallback = preferred == OtpChannel.WhatsApp ? OtpChannel.Sms : OtpChannel.WhatsApp;
        if (IsEnabled(preferred))
        {
            return preferred;
        }

        return IsEnabled(fallback) ? fallback : throw new ServiceUnavailableCoreException(setup.ChannelUnavailableErrorCode);
    }

    private bool IsEnabled(OtpChannel channel) => channel switch
    {
        OtpChannel.WhatsApp => _options.WhatsApp.Enabled,
        OtpChannel.Sms => _options.Sms.Enabled,
        OtpChannel.Email => _options.Email.Enabled,
        _ => false,
    };
}
