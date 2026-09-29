using System.Text.Json.Serialization;

namespace Elmanhg.Infrastructure.Payments.Paymob;

public sealed record PaymobIntentionResponse([property: JsonPropertyName("client_secret")] string? ClientSecret);
