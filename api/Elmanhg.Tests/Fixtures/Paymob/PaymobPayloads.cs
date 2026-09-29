using Elmanhg.Infrastructure.Payments.Paymob;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Elmanhg.Tests.Fixtures.Paymob;

public static class PaymobPayloads
{
    public const string Succeeded = "transaction-succeeded.json";
    public const string Declined = "transaction-declined.json";
    public const string Pending = "transaction-pending.json";
    public const string Refunded = "transaction-refunded.json";
    public const string RefundChild = "transaction-refund.json";
    public const string Token = "token.json";

    public static string Read(string fileName) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Paymob", fileName));

    public static string ForPayment(string fileName, Guid paymentId, long transactionId, long amountCents, long orderId)
    {
        var root = JsonNode.Parse(Read(fileName))!;
        var transaction = root["obj"]!;
        transaction["id"] = transactionId;
        transaction["amount_cents"] = amountCents;
        var order = transaction["order"]!;
        order["id"] = orderId;
        order["merchant_order_id"] = paymentId.ToString();
        order["amount_cents"] = amountCents;
        return root.ToJsonString();
    }

    public static string Sign(string payload, string secret) => PaymobHmac.Compute(JsonDocument.Parse(payload).RootElement.GetProperty("obj"), secret);
}
