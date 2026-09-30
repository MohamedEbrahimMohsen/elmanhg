namespace Elmanhg.Domain.Analytics;

public sealed record FunnelTiming
{
    public int Completed { get; init; }
    public double? MedianSeconds { get; init; }
}
