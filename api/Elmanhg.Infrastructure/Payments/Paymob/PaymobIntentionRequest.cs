using Elmanhg.Application.Shared.Payments;
using System.Text.Json.Serialization;

namespace Elmanhg.Infrastructure.Payments.Paymob;

public sealed record PaymobIntentionRequest([property: JsonPropertyName("amount")] long Amount, [property: JsonPropertyName("currency")] string Currency, [property: JsonPropertyName("payment_methods")] List<int> PaymentMethods, [property: JsonPropertyName("items")] List<PaymobItem> Items, [property: JsonPropertyName("billing_data")] PaymobBillingData BillingData, [property: JsonPropertyName("special_reference")] string SpecialReference, [property: JsonPropertyName("notification_url"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? NotificationUrl, [property: JsonPropertyName("redirection_url")] string RedirectionUrl)
{
    private const int SingleQuantity = 1;

    public static PaymobIntentionRequest Create(PaymentCheckoutRequest request, PaymobOptions options)
    {
        var itemName = $"Elmanhg {request.Plan} {request.Period}";
        return new(
            Amount: request.Amount.AmountMinor,
            Currency: request.Amount.Currency,
            PaymentMethods: [.. options.IntegrationIds],
            Items: [new(itemName, request.Amount.AmountMinor, itemName, SingleQuantity)],
            BillingData: PaymobBillingData.From(request.Customer, options.BillingCountry),
            SpecialReference: request.PaymentId.ToString(),
            NotificationUrl: string.IsNullOrWhiteSpace(options.NotificationUrl) ? null : options.NotificationUrl,
            RedirectionUrl: $"{options.RedirectionUrl.TrimEnd('/')}/{request.PaymentId}");
    }
}

public sealed record PaymobItem([property: JsonPropertyName("name")] string Name, [property: JsonPropertyName("amount")] long Amount, [property: JsonPropertyName("description")] string Description, [property: JsonPropertyName("quantity")] int Quantity);
