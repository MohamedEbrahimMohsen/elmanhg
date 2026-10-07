namespace Core.DDD.Time;

public static class DateTimeOffsetExtensions
{
    public static DateTimeOffset TruncateToMicroseconds(this DateTimeOffset value) => value.AddTicks(-(value.Ticks % TimeSpan.TicksPerMicrosecond));
}
