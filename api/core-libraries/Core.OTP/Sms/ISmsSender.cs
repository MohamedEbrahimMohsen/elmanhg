namespace Core.OTP.Sms;

public interface ISmsSender
{
    Task SendOtpAsync(string phoneNumber, string code, CancellationToken cancellationToken);
}
