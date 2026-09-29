namespace Elmanhg.Application.Shared.Payments;

public sealed record PaymentCheckout(string RedirectUrl, string? ProviderOrderId = null);
