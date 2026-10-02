using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Shared.Options;

public sealed class SlaCalendarOptionsValidator : IValidateOptions<SlaCalendarOptions>
{
    public ValidateOptionsResult Validate(string? name, SlaCalendarOptions options)
    {
        var weekendDays = options.WeekendDayNames();
        var allowedTimeZones = options.AllowedTimeZoneIds();
        List<string> failures = [];
        if (!weekendDays.All(x => Enum.GetNames<DayOfWeek>().Contains(x, StringComparer.Ordinal)))
        {
            failures.Add("SlaCalendar:WeekendDays must list day names (Sunday … Saturday).");
        }

        if (weekendDays.Distinct(StringComparer.Ordinal).Count() != weekendDays.Count)
        {
            failures.Add("SlaCalendar:WeekendDays must not repeat a day.");
        }

        if (weekendDays.Count >= Enum.GetNames<DayOfWeek>().Length)
        {
            failures.Add("SlaCalendar:WeekendDays must leave at least one day that counts.");
        }

        failures.AddRange(allowedTimeZones
            .Where(x => !TimeZoneInfo.TryFindSystemTimeZoneById(x, out _))
            .Select(x => $"SlaCalendar:AllowedTimeZones has an unknown time zone id {x}."));
        if (!allowedTimeZones.Contains(options.TimeZone, StringComparer.Ordinal))
        {
            failures.Add("SlaCalendar:TimeZone must be one of SlaCalendar:AllowedTimeZones.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
