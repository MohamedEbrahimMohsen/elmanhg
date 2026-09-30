using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.SharedKernel;

namespace Elmanhg.Application.Dashboard.Shared;

public sealed record DashboardWindow(DateOnly From, DateOnly To, DateOnly Today, DateTimeOffset Now, string TimeZone)
{
    public DateTimeOffset Start => StartOfDay(From);

    public DateTimeOffset End => StartOfDay(To.AddDays(1));

    public int DayCount => To.DayNumber - From.DayNumber + 1;

    public IEnumerable<DateOnly> Days => Enumerable.Range(0, Math.Max(DayCount, 0)).Select(From.AddDays);

    public MetricsWindow ToMetricsWindow() => new(Start, End, TimeZone);

    public DateTimeOffset StartOfDay(DateOnly day)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(TimeZone);
        var wallClock = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        // Egypt springs forward at local midnight, so that midnight does not exist; ConvertTimeToUtc would throw on it.
        var offset = zone.GetUtcOffset(wallClock - zone.GetUtcOffset(wallClock));
        return (wallClock - offset).ToUniversalTime();
    }

    public static DateOnly LocalDay(DateTimeOffset now, string timeZone) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(timeZone)).DateTime);

    public static DashboardWindow Resolve(DateOnly? from, DateOnly? to, DateTimeOffset now, DashboardOptions options)
    {
        var today = LocalDay(now, options.TimeZone);
        var resolvedTo = to ?? today;
        var resolvedFrom = from ?? resolvedTo.AddDays(-(options.DefaultRangeDays - 1));
        return new DashboardWindow(resolvedFrom, resolvedTo, today, now, options.TimeZone);
    }
}
