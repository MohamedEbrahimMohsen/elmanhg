namespace Core.OTP.Delivery;

public interface IOtpChannel
{
    OtpChannel Channel { get; }

    Task SendAsync(string recipient, string code, CancellationToken cancellationToken);
}
