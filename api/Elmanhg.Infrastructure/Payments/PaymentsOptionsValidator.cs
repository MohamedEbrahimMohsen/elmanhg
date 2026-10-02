using Elmanhg.Infrastructure.Payments.Paymob;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Elmanhg.Infrastructure.Payments;

public sealed class PaymentsOptionsValidator(IHostEnvironment hostEnvironment) : IValidateOptions<PaymentsOptions>
{
    public ValidateOptionsResult Validate(string? name, PaymentsOptions options)
    {
        List<string> failures = [];
        if (options.AttemptTimeoutSeconds > options.TotalTimeoutSeconds)
        {
            failures.Add("Payments:AttemptTimeoutSeconds must not exceed Payments:TotalTimeoutSeconds.");
        }

        if (options.AllowFakePayments && hostEnvironment.IsProduction())
        {
            failures.Add("Payments:AllowFakePayments must not be true in Production: the fake gateway grants plans without payment.");
        }

        if (options.Provider == PaymentProvider.Paymob)
        {
            ValidatePaymob(options.Paymob, failures);
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidatePaymob(PaymobOptions paymob, List<string> failures)
    {
        List<(string Key, string Value)> required = [("SecretKey", paymob.SecretKey), ("PublicKey", paymob.PublicKey), ("RedirectionUrl", paymob.RedirectionUrl), ("BillingCountry", paymob.BillingCountry), ("HmacSecret", paymob.HmacSecret)];
        failures.AddRange(required
            .Where(x => string.IsNullOrWhiteSpace(x.Value))
            .Select(x => $"Payments:Paymob:{x.Key} is required when the Paymob provider is selected."));
        if (paymob.IntegrationIds.Count == 0 || paymob.IntegrationIds.Any(x => x <= 0))
        {
            failures.Add("Payments:Paymob:IntegrationIds needs at least one positive integration id.");
        }

        RequireHttps("BaseUrl", paymob.BaseUrl, failures);
        RequireHttps("CheckoutUrl", paymob.CheckoutUrl, failures);
        RequireHttps("RedirectionUrl", paymob.RedirectionUrl, failures);
        if (!string.IsNullOrWhiteSpace(paymob.NotificationUrl))
        {
            RequireHttps("NotificationUrl", paymob.NotificationUrl, failures);
        }
    }

    private static void RequireHttps(string key, string value, List<string> failures)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            failures.Add($"Payments:Paymob:{key} must be an absolute https URL.");
        }
    }
}
