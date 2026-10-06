namespace Core.OTP.Delivery;

public sealed record OtpDeliverySetup(string DeliveryFailedErrorCode, string ChannelUnavailableErrorCode, Func<string, int, OtpEmailContent> RenderEmail);
