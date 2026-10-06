using Core.Utilities.Time;
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

    public DateTimeOffset StartOfDay(DateOnly day) => TimeZoneInfo.FindSystemTimeZoneById(TimeZone).StartOfDay(day);

    public static DateOnly LocalDay(DateTimeOffset now, string timeZone) => TimeZoneInfo.FindSystemTimeZoneById(timeZone).LocalDate(now);

    public static DashboardWindow Resolve(DateOnly? from, DateOnly? to, DateTimeOffset now, DashboardOptions options)
    {
        var today = LocalDay(now, options.TimeZone);
        var resolvedTo = to ?? today;
        var resolvedFrom = from ?? resolvedTo.AddDays(-(options.DefaultRangeDays - 1));
        return new DashboardWindow(resolvedFrom, resolvedTo, today, now, options.TimeZone);
    }
}
