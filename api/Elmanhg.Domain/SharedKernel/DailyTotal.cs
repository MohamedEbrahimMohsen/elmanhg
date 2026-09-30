namespace Elmanhg.Domain.SharedKernel;

public sealed record DailyTotal
{
    public DateOnly Day { get; init; }
    public long Value { get; init; }
}
