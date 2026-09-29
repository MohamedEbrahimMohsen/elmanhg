using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Elmanhg.Infrastructure.Payments.Paymob;

public static class PaymobHmac
{
    // Paymob's documented transaction-callback field set; order is part of the signature.
    public static readonly IReadOnlyList<string> TransactionFields = ["amount_cents", "created_at", "currency", "error_occured", "has_parent_transaction", "id", "integration_id", "is_3d_secure", "is_auth", "is_capture", "is_refunded", "is_standalone_payment", "is_voided", "order.id", "owner", "pending", "source_data.pan", "source_data.sub_type", "source_data.type", "success"];

    public static string Compute(JsonElement transaction, string secret)
    {
        var message = string.Concat(TransactionFields.Select(x => ValueAt(transaction, x)));
        return Convert.ToHexStringLower(HMACSHA512.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(message)));
    }

    public static bool IsValid(JsonElement transaction, string secret, string? signature)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Compute(transaction, secret)), Encoding.UTF8.GetBytes(signature.Trim().ToLowerInvariant()));
    }

    private static string ValueAt(JsonElement transaction, string path)
    {
        var current = transaction;
        foreach (var segment in path.Split('.'))
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                return string.Empty;
            }
        }

        return current.ValueKind switch
        {
            JsonValueKind.String => current.GetString() ?? string.Empty,
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Number => current.GetRawText(),
            _ => string.Empty,
        };
    }
}
