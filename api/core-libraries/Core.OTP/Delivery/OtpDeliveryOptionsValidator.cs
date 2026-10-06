using Core.Messaging.Sms;
using Core.OTP.Delivery.Email;
using Core.OTP.Delivery.Sms;
using Core.OTP.Delivery.WhatsApp;
using Microsoft.Extensions.Options;

namespace Core.OTP.Delivery;

public sealed class OtpDeliveryOptionsValidator : IValidateOptions<OtpDeliveryOptions>
{
    public ValidateOptionsResult Validate(string? name, OtpDeliveryOptions options)
    {
        List<string> failures = [];
        ValidateRouting(options, failures);
        ValidateWhatsApp(options.WhatsApp, failures);
        ValidateEmail(options.Email, failures);
        ValidateSms(options.Sms, failures);
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateRouting(OtpDeliveryOptions options, List<string> failures)
    {
        if (options.DefaultPhoneChannel == OtpChannel.Email)
        {
            failures.Add("OtpDelivery:DefaultPhoneChannel must be WhatsApp or Sms.");
        }

        if (options.AttemptTimeoutSeconds > options.TotalTimeoutSeconds)
        {
            failures.Add("OtpDelivery:AttemptTimeoutSeconds must not exceed OtpDelivery:TotalTimeoutSeconds.");
        }
    }

    private static void ValidateWhatsApp(WhatsAppOtpOptions whatsApp, List<string> failures)
    {
        if (!whatsApp.Enabled || whatsApp.Provider != WhatsAppProvider.Meta)
        {
            return;
        }

        Require("WhatsApp", "Meta", [("BaseUrl", whatsApp.BaseUrl), ("ApiVersion", whatsApp.ApiVersion), ("PhoneNumberId", whatsApp.PhoneNumberId), ("AccessToken", whatsApp.AccessToken), ("TemplateName", whatsApp.TemplateName), ("LanguageCode", whatsApp.LanguageCode)], failures);
        RequireHttps("OtpDelivery:WhatsApp:BaseUrl", whatsApp.BaseUrl, failures);
    }

    private static void ValidateEmail(EmailOtpOptions email, List<string> failures)
    {
        if (!email.Enabled || email.Provider != EmailProvider.Resend)
        {
            return;
        }

        Require("Email", "Resend", [("BaseUrl", email.BaseUrl), ("ApiKey", email.ApiKey), ("FromAddress", email.FromAddress), ("Subject", email.Subject)], failures);
        RequireHttps("OtpDelivery:Email:BaseUrl", email.BaseUrl, failures);
    }

    private static void ValidateSms(SmsOtpOptions sms, List<string> failures)
    {
        if (!sms.Enabled || sms.Provider != SmsProvider.Http)
        {
            return;
        }

        Require("Sms", "Http", [("Url", sms.Url), ("ContentType", sms.ContentType), ("BodyTemplate", sms.BodyTemplate), ("MessageTemplate", sms.MessageTemplate)], failures);
        RequireHttps("OtpDelivery:Sms:Url", sms.Url, failures);
        if (sms.ContentType is not (HttpSmsBodyRenderer.JsonContentType or HttpSmsBodyRenderer.FormContentType))
        {
            failures.Add("OtpDelivery:Sms:ContentType must be application/json or application/x-www-form-urlencoded.");
        }

        if (!sms.BodyTemplate.Contains(HttpSmsBodyRenderer.MessageToken, StringComparison.Ordinal) || !(sms.BodyTemplate.Contains(HttpSmsBodyRenderer.PhoneNumberToken, StringComparison.Ordinal) || sms.BodyTemplate.Contains(HttpSmsBodyRenderer.InternationalPhoneNumberToken, StringComparison.Ordinal)))
        {
            failures.Add("OtpDelivery:Sms:BodyTemplate must contain {message} and {phoneNumber} or {internationalPhoneNumber}.");
        }

        if (!sms.MessageTemplate.Contains(HttpSmsOtpChannel.CodeToken, StringComparison.Ordinal))
        {
            failures.Add("OtpDelivery:Sms:MessageTemplate must contain {code}.");
        }

        if (string.IsNullOrWhiteSpace(sms.AuthHeaderName) != string.IsNullOrWhiteSpace(sms.AuthHeaderValue))
        {
            failures.Add("OtpDelivery:Sms:AuthHeaderName and AuthHeaderValue must be set together.");
        }
    }

    private static void Require(string section, string provider, List<(string Key, string Value)> values, List<string> failures)
    {
        failures.AddRange(values
            .Where(x => string.IsNullOrWhiteSpace(x.Value))
            .Select(x => $"OtpDelivery:{section}:{x.Key} is required when the {provider} provider is enabled."));
    }

    private static void RequireHttps(string key, string value, List<string> failures)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            failures.Add($"{key} must be an absolute https URL.");
        }
    }
}
