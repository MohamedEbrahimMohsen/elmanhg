using System.Globalization;

namespace Elmanhg.Infrastructure.Messaging;

public static class ReminderDeadlineFormatter
{
    public const string DeadlineFormat = "yyyy-MM-dd HH:mm";

    public static string Format(DateTimeOffset instant, string timeZoneId) => TimeZoneInfo.ConvertTime(instant, TimeZoneInfo.FindSystemTimeZoneById(timeZoneId)).ToString(DeadlineFormat, CultureInfo.InvariantCulture);
}
