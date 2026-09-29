using System.Text.Json;
using System.Text.Json.Serialization;

namespace Elmanhg.Infrastructure.Payments.Paymob;

public sealed record PaymobRefundResponse([property: JsonPropertyName("id")] JsonElement? Id, [property: JsonPropertyName("success")] bool? Success, [property: JsonPropertyName("pending")] bool? Pending);
