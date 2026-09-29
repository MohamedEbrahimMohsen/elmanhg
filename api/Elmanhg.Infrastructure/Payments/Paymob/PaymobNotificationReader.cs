using Core.Errors;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Payments;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Elmanhg.Infrastructure.Payments.Paymob;

public sealed class PaymobNotificationReader(IOptions<PaymentsOptions> paymentsOptions) : IPaymentNotificationReader
{
    private const string TransactionType = "TRANSACTION";
    private const string TypeProperty = "type";
    private const string TransactionProperty = "obj";
    private const string SignatureProperty = "hmac";
    private const string IdProperty = "id";
    private const string SuccessProperty = "success";
    private const string PendingProperty = "pending";
    private const string AmountProperty = "amount_cents";
    private const string CurrencyProperty = "currency";
    private const string OrderProperty = "order";
    private const string MerchantOrderIdProperty = "merchant_order_id";
    private const string HasParentProperty = "has_parent_transaction";
    private const string IsCaptureProperty = "is_capture";
    private const string IsRefundedProperty = "is_refunded";
    private const string IsVoidedProperty = "is_voided";

    public PaymentNotification? Read(string payload, string? signature)
    {
        using var document = Parse(payload);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new BadRequestCoreException(ErrorCodes.PaymobWebhookPayloadInvalid);
        }

        if (!root.TryGetProperty(TypeProperty, out var type) || type.ValueKind != JsonValueKind.String || type.GetString() != TransactionType)
        {
            return null;
        }

        if (!root.TryGetProperty(TransactionProperty, out var transaction) || transaction.ValueKind != JsonValueKind.Object)
        {
            throw new BadRequestCoreException(ErrorCodes.PaymobWebhookPayloadInvalid);
        }

        var providedSignature = signature ?? (root.TryGetProperty(SignatureProperty, out var bodySignature) && bodySignature.ValueKind == JsonValueKind.String ? bodySignature.GetString() : null);
        if (!PaymobHmac.IsValid(transaction, paymentsOptions.Value.Paymob.HmacSecret, providedSignature))
        {
            throw new UnauthorizedCoreException(ErrorCodes.PaymobWebhookSignatureInvalid);
        }

        return ToNotification(transaction);
    }

    private static JsonDocument Parse(string payload)
    {
        try
        {
            return JsonDocument.Parse(payload);
        }
        catch (JsonException exception)
        {
            throw new BadRequestCoreException(ErrorCodes.PaymobWebhookPayloadInvalid, innerException: exception);
        }
    }

    private static PaymentNotification ToNotification(JsonElement transaction)
    {
        var order = transaction.TryGetProperty(OrderProperty, out var value) && value.ValueKind == JsonValueKind.Object ? value : throw new BadRequestCoreException(ErrorCodes.PaymobWebhookPayloadInvalid);
        var transactionId = Identifier(transaction, IdProperty) ?? throw new BadRequestCoreException(ErrorCodes.PaymobWebhookPayloadInvalid);
        var orderId = Identifier(order, IdProperty) ?? throw new BadRequestCoreException(ErrorCodes.PaymobWebhookPayloadInvalid);
        var amount = transaction.TryGetProperty(AmountProperty, out var amountValue) && amountValue.ValueKind == JsonValueKind.Number && amountValue.TryGetInt64(out var amountMinor) ? amountMinor : throw new BadRequestCoreException(ErrorCodes.PaymobWebhookPayloadInvalid);
        var currency = transaction.TryGetProperty(CurrencyProperty, out var currencyValue) && currencyValue.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(currencyValue.GetString()) ? currencyValue.GetString()! : throw new BadRequestCoreException(ErrorCodes.PaymobWebhookPayloadInvalid);
        return new PaymentNotification(transactionId, Identifier(order, MerchantOrderIdProperty), orderId, Flag(transaction, SuccessProperty), Flag(transaction, PendingProperty), KindOf(transaction), amount, currency);
    }

    // Only HMAC-signed fields classify a callback; is_refund/is_void are unsigned.
    private static PaymentNotificationKind KindOf(JsonElement transaction) => (OptionalFlag(transaction, HasParentProperty), OptionalFlag(transaction, IsCaptureProperty)) switch
    {
        (true, false) => PaymentNotificationKind.Reversal,
        (true, true) => PaymentNotificationKind.Other,
        _ when OptionalFlag(transaction, IsRefundedProperty) || OptionalFlag(transaction, IsVoidedProperty) => PaymentNotificationKind.Other,
        _ => PaymentNotificationKind.Charge,
    };

    private static bool OptionalFlag(JsonElement element, string property) => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;

    private static bool Flag(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : throw new BadRequestCoreException(ErrorCodes.PaymobWebhookPayloadInvalid);
    }

    private static string? Identifier(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.String when !string.IsNullOrWhiteSpace(value.GetString()) => value.GetString(),
            _ => null,
        };
    }
}
