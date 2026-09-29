namespace Elmanhg.Infrastructure.Payments.Paymob;

public sealed class PaymobOptions
{
    public string BaseUrl { get; set; } = "https://accept.paymob.com";
    public string CheckoutUrl { get; set; } = "https://accept.paymob.com/unifiedcheckout/";
    public string SecretKey { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public List<int> IntegrationIds { get; set; } = [];
    public string NotificationUrl { get; set; } = string.Empty;
    public string RedirectionUrl { get; set; } = string.Empty;
    public string BillingCountry { get; set; } = "EG";
    public string HmacSecret { get; set; } = string.Empty;
}
