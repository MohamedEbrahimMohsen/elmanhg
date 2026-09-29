using System.Text.Json.Serialization;

namespace Elmanhg.Infrastructure.Payments.Paymob;

public sealed record PaymobRefundRequest([property: JsonPropertyName("transaction_id")] string TransactionId, [property: JsonPropertyName("amount_cents")] long AmountCents);
