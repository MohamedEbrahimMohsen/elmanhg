using Elmanhg.Domain.Subscriptions;

namespace Elmanhg.Application.Subscriptions.Shared;

public static class PaymentResultGenerator
{
    public static PaymentResult Generate(Payment payment) => new(payment.Id, payment.Plan, payment.Period, payment.Amount, payment.Status, payment.CreationDate, payment.CompletedAt);
}
