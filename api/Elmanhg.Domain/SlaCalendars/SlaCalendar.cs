using Core.DDD.Time;

namespace Elmanhg.Domain.SlaCalendars;

public sealed class SlaCalendar(bool skipWeekends, IReadOnlyCollection<DayOfWeek> weekendDays, TimeZoneInfo timeZone, IReadOnlyList<SlaDateRange> examPeriods)
{
    // A calendar with no counted day would never end a window; the runtime constraint keeps one weekday counted, so this bound is a guard, not a limit.
    public const int MaxDaysScanned = 3660;

    public bool SkipWeekends { get; } = skipWeekends;
    public IReadOnlyCollection<DayOfWeek> WeekendDays { get; } = weekendDays;
    public TimeZoneInfo TimeZone { get; } = timeZone;
    public IReadOnlyList<SlaDateRange> ExamPeriods { get; } = examPeriods;

    public bool Counts(DateOnly day) => !SkipWeekends || !WeekendDays.Contains(day.DayOfWeek) || ExamPeriods.Any(x => x.Contains(day));

    public DateOnly LocalDate(DateTimeOffset instant) => TimeZone.LocalDate(instant);

    public DateTimeOffset StartOfDay(DateOnly day) => TimeZone.StartOfDay(day);

    public DateTimeOffset AddCountedTime(DateTimeOffset start, TimeSpan duration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero);
        var cursor = start.ToUniversalTime();
        var remaining = duration;
        var day = LocalDate(cursor);
        for (var scanned = 0; scanned < MaxDaysScanned; scanned++)
        {
            var dayEnd = StartOfDay(day.AddDays(1));
            if (Counts(day))
            {
                var available = dayEnd - cursor;
                if (remaining <= available)
                {
                    return cursor + remaining;
                }

                remaining -= available;
            }

            cursor = dayEnd;
            day = day.AddDays(1);
        }

        throw new InvalidOperationException("The SLA calendar has no day that counts.");
    }
}
