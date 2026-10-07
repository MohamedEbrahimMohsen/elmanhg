namespace Core.OTP.Delivery;

public interface IOtpDeliveryObserver
{
    void RecordOtpSend(OtpChannel channel, bool delivered);
}
