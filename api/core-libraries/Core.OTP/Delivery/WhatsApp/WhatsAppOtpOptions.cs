namespace Core.OTP.Delivery.WhatsApp;

public sealed class WhatsAppOtpOptions
{
    public bool Enabled { get; set; }
    public WhatsAppProvider Provider { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiVersion { get; set; } = string.Empty;
    public string PhoneNumberId { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string LanguageCode { get; set; } = string.Empty;
    public bool CopyCodeButton { get; set; }
}
