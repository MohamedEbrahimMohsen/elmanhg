using System.Text.Json;
using System.Text.Json.Serialization;

namespace Elmanhg.Infrastructure.Payments.Paymob;

public sealed record PaymobIntentionResponse([property: JsonPropertyName("client_secret")] string? ClientSecret, [property: JsonPropertyName("intention_order_id")] JsonElement? IntentionOrderId);
