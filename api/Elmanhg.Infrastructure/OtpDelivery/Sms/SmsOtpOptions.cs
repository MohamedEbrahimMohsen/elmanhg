namespace Elmanhg.Infrastructure.OtpDelivery.Sms;

public sealed class SmsOtpOptions
{
    public bool Enabled { get; set; }
    public SmsProvider Provider { get; set; }
    public string Url { get; set; } = string.Empty;
    public string AuthHeaderName { get; set; } = string.Empty;
    public string AuthHeaderValue { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string BodyTemplate { get; set; } = string.Empty;
    public string MessageTemplate { get; set; } = string.Empty;
}
