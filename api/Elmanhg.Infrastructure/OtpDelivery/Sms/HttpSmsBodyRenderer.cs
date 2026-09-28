using System.Text.Encodings.Web;
using System.Text.Json;

namespace Elmanhg.Infrastructure.OtpDelivery.Sms;

public static class HttpSmsBodyRenderer
{
    public const string JsonContentType = "application/json";
    public const string FormContentType = "application/x-www-form-urlencoded";
    public const string PhoneNumberToken = "{phoneNumber}";
    public const string InternationalPhoneNumberToken = "{internationalPhoneNumber}";
    public const string MessageToken = "{message}";

    public static string Render(string bodyTemplate, string contentType, string phoneNumber, string internationalPhoneNumber, string message) => bodyTemplate
        .Replace(PhoneNumberToken, Encode(phoneNumber, contentType), StringComparison.Ordinal)
        .Replace(InternationalPhoneNumberToken, Encode(internationalPhoneNumber, contentType), StringComparison.Ordinal)
        .Replace(MessageToken, Encode(message, contentType), StringComparison.Ordinal);

    private static string Encode(string value, string contentType) => contentType == JsonContentType ? JsonEncodedText.Encode(value, JavaScriptEncoder.UnsafeRelaxedJsonEscaping).ToString() : Uri.EscapeDataString(value);
}
