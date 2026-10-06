using Core.OTP.Delivery.Email;
using Core.OTP.Delivery.Sms;
using Core.OTP.Delivery.WhatsApp;

namespace Core.OTP.Delivery;

public static class OtpDeliveryProviders
{
    public static bool UsesMeta(WhatsAppOtpOptions whatsApp) => whatsApp switch
    {
        { Enabled: false } or { Provider: WhatsAppProvider.Fake } => false,
        { Provider: WhatsAppProvider.Meta } => true,
        _ => throw new InvalidOperationException("Unsupported OtpDelivery:WhatsApp:Provider."),
    };

    public static bool UsesResend(EmailOtpOptions email) => email switch
    {
        { Enabled: false } or { Provider: EmailProvider.Fake } => false,
        { Provider: EmailProvider.Resend } => true,
        _ => throw new InvalidOperationException("Unsupported OtpDelivery:Email:Provider."),
    };

    public static bool UsesHttp(SmsOtpOptions sms) => sms switch
    {
        { Enabled: false } or { Provider: SmsProvider.Fake } => false,
        { Provider: SmsProvider.Http } => true,
        _ => throw new InvalidOperationException("Unsupported OtpDelivery:Sms:Provider."),
    };
}
