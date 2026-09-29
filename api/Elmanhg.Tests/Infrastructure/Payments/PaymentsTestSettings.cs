using Elmanhg.Infrastructure.Payments;
using System.Globalization;

namespace Elmanhg.Tests.Infrastructure.Payments;

public static class PaymentsTestSettings
{
    public static PaymentsOptions Fake() => new();

    public static PaymentsOptions WithPaymob()
    {
        var options = Fake();
        options.Provider = PaymentProvider.Paymob;
        options.Paymob.SecretKey = "sk_test";
        options.Paymob.PublicKey = "pk_test";
        options.Paymob.IntegrationIds = [111, 222];
        options.Paymob.RedirectionUrl = "https://app.test/student/checkout-result";
        options.Paymob.NotificationUrl = "https://api.test/api/payments/paymob/webhook";
        options.Paymob.HmacSecret = "hmac_test";
        return options;
    }

    public static Dictionary<string, string?> ToConfiguration(PaymentsOptions options)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Payments:Provider"] = options.Provider.ToString(),
            ["Payments:FakeCheckoutPath"] = options.FakeCheckoutPath,
            ["Payments:AttemptTimeoutSeconds"] = options.AttemptTimeoutSeconds.ToString(CultureInfo.InvariantCulture),
            ["Payments:TotalTimeoutSeconds"] = options.TotalTimeoutSeconds.ToString(CultureInfo.InvariantCulture),
            ["Payments:Paymob:BaseUrl"] = options.Paymob.BaseUrl,
            ["Payments:Paymob:CheckoutUrl"] = options.Paymob.CheckoutUrl,
            ["Payments:Paymob:SecretKey"] = options.Paymob.SecretKey,
            ["Payments:Paymob:PublicKey"] = options.Paymob.PublicKey,
            ["Payments:Paymob:NotificationUrl"] = options.Paymob.NotificationUrl,
            ["Payments:Paymob:RedirectionUrl"] = options.Paymob.RedirectionUrl,
            ["Payments:Paymob:BillingCountry"] = options.Paymob.BillingCountry,
            ["Payments:Paymob:HmacSecret"] = options.Paymob.HmacSecret,
        };
        for (var index = 0; index < options.Paymob.IntegrationIds.Count; index++)
        {
            settings[$"Payments:Paymob:IntegrationIds:{index}"] = options.Paymob.IntegrationIds[index].ToString(CultureInfo.InvariantCulture);
        }

        return settings;
    }
}
