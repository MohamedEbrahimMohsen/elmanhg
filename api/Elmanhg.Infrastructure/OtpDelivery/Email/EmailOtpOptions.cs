namespace Elmanhg.Infrastructure.OtpDelivery.Email;

public sealed class EmailOtpOptions
{
    public bool Enabled { get; set; }
    public EmailProvider Provider { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
}
