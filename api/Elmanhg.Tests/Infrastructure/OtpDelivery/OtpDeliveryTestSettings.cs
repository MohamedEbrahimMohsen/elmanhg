using Core.Messaging.Sms;
using Core.OTP.Delivery;
using Core.OTP.Delivery.Email;
using Core.OTP.Delivery.Sms;
using Core.OTP.Delivery.WhatsApp;
using Elmanhg.Application.Exceptions;
using Elmanhg.Infrastructure.OtpDelivery.Email;
using System.Globalization;

namespace Elmanhg.Tests.Infrastructure.OtpDelivery;

public static class OtpDeliveryTestSettings
{
    public static OtpDeliveryOptions Fake() => new()
    {
        DefaultPhoneChannel = OtpChannel.WhatsApp,
        CountryCallingCode = "20",
        AttemptTimeoutSeconds = 10,
        TotalTimeoutSeconds = 30,
        WhatsApp = new() { Enabled = true, Provider = WhatsAppProvider.Fake, BaseUrl = "https://graph.facebook.com", ApiVersion = "v23.0", LanguageCode = "ar", CopyCodeButton = true },
        Email = new() { Enabled = true, Provider = EmailProvider.Fake, BaseUrl = "https://api.resend.com", Subject = "رمز الدخول إلى المنهج" },
        Sms = new() { Enabled = false, Provider = SmsProvider.Fake, ContentType = HttpSmsBodyRenderer.JsonContentType, MessageTemplate = "رمز الدخول إلى المنهج: {code}" },
    };

    public static OtpDeliveryOptions WithMeta()
    {
        var options = Fake();
        options.WhatsApp.Provider = WhatsAppProvider.Meta;
        options.WhatsApp.PhoneNumberId = "123456";
        options.WhatsApp.AccessToken = "meta-token";
        options.WhatsApp.TemplateName = "elmanhg_otp";
        return options;
    }

    public static OtpDeliveryOptions WithResend()
    {
        var options = Fake();
        options.Email.Provider = EmailProvider.Resend;
        options.Email.ApiKey = "re_test";
        options.Email.FromAddress = "Elmanhg <otp@elmanhg.test>";
        return options;
    }

    public static OtpDeliveryOptions WithHttpSms()
    {
        var options = Fake();
        options.Sms.Enabled = true;
        options.Sms.Provider = SmsProvider.Http;
        options.Sms.Url = "https://sms.example.test/send";
        options.Sms.AuthHeaderName = "X-Api-Key";
        options.Sms.AuthHeaderValue = "sms-key";
        options.Sms.BodyTemplate = "{\"to\":\"{internationalPhoneNumber}\",\"text\":\"{message}\"}";
        return options;
    }

    public static OtpDeliverySetup Setup() => new(ErrorCodes.OtpDeliveryFailed, ErrorCodes.OtpChannelUnavailable, OtpEmailTemplate.Render);

    public static Dictionary<string, string?> ToConfiguration(OtpDeliveryOptions options) => new()
    {
        ["OtpDelivery:DefaultPhoneChannel"] = options.DefaultPhoneChannel?.ToString(),
        ["OtpDelivery:CountryCallingCode"] = options.CountryCallingCode,
        ["OtpDelivery:AttemptTimeoutSeconds"] = options.AttemptTimeoutSeconds.ToString(CultureInfo.InvariantCulture),
        ["OtpDelivery:TotalTimeoutSeconds"] = options.TotalTimeoutSeconds.ToString(CultureInfo.InvariantCulture),
        ["OtpDelivery:WhatsApp:Enabled"] = options.WhatsApp.Enabled.ToString(),
        ["OtpDelivery:WhatsApp:Provider"] = options.WhatsApp.Provider.ToString(),
        ["OtpDelivery:WhatsApp:BaseUrl"] = options.WhatsApp.BaseUrl,
        ["OtpDelivery:WhatsApp:ApiVersion"] = options.WhatsApp.ApiVersion,
        ["OtpDelivery:WhatsApp:PhoneNumberId"] = options.WhatsApp.PhoneNumberId,
        ["OtpDelivery:WhatsApp:AccessToken"] = options.WhatsApp.AccessToken,
        ["OtpDelivery:WhatsApp:TemplateName"] = options.WhatsApp.TemplateName,
        ["OtpDelivery:WhatsApp:LanguageCode"] = options.WhatsApp.LanguageCode,
        ["OtpDelivery:WhatsApp:CopyCodeButton"] = options.WhatsApp.CopyCodeButton.ToString(),
        ["OtpDelivery:Email:Enabled"] = options.Email.Enabled.ToString(),
        ["OtpDelivery:Email:Provider"] = options.Email.Provider.ToString(),
        ["OtpDelivery:Email:BaseUrl"] = options.Email.BaseUrl,
        ["OtpDelivery:Email:ApiKey"] = options.Email.ApiKey,
        ["OtpDelivery:Email:FromAddress"] = options.Email.FromAddress,
        ["OtpDelivery:Email:Subject"] = options.Email.Subject,
        ["OtpDelivery:Sms:Enabled"] = options.Sms.Enabled.ToString(),
        ["OtpDelivery:Sms:Provider"] = options.Sms.Provider.ToString(),
        ["OtpDelivery:Sms:Url"] = options.Sms.Url,
        ["OtpDelivery:Sms:AuthHeaderName"] = options.Sms.AuthHeaderName,
        ["OtpDelivery:Sms:AuthHeaderValue"] = options.Sms.AuthHeaderValue,
        ["OtpDelivery:Sms:ContentType"] = options.Sms.ContentType,
        ["OtpDelivery:Sms:BodyTemplate"] = options.Sms.BodyTemplate,
        ["OtpDelivery:Sms:MessageTemplate"] = options.Sms.MessageTemplate,
    };
}
