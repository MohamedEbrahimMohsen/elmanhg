namespace Elmanhg.Application.Shared.Payments;

public sealed record PaymentNotification(string TransactionId, string? MerchantOrderId, string? ProviderOrderId, bool Succeeded, bool Pending, bool IsRefundOrVoid, long AmountMinor, string Currency);
