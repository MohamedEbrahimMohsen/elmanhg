namespace Core.DDD.Time;

public static class TimeZoneInfoExtensions
{
    public static DateOnly LocalDate(this TimeZoneInfo zone, DateTimeOffset instant) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);

    public static DateTimeOffset StartOfDay(this TimeZoneInfo zone, DateOnly day)
    {
        var wallClock = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        // Where a zone springs forward at local midnight that midnight does not exist; the offset in force just before it gives the transition instant.
        var offset = zone.GetUtcOffset(wallClock - zone.GetUtcOffset(wallClock));
        return (wallClock - offset).ToUniversalTime();
    }
}
