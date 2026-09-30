namespace Elmanhg.Domain.Subscriptions;

public sealed record PaymentTotals
{
    public int Succeeded { get; init; }
    public int Failed { get; init; }
    public long GrossMinor { get; init; }
    public int Refunds { get; init; }
    public long RefundedMinor { get; init; }
}
