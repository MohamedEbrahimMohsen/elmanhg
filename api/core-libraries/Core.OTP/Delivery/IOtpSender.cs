using Core.OTP.Entities;

namespace Core.OTP.Delivery;

public interface IOtpSender
{
    Task<OtpChannel> SendAsync(OtpRecipientType recipientType, string recipient, string code, CancellationToken cancellationToken);
}
