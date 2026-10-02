using Elmanhg.Domain.SlaCalendars;
using Elmanhg.Domain.TeacherThreads;

namespace Elmanhg.Tests.Builders;

public static class TeacherThreadSlaPolicies
{
    public static TimeZoneInfo Cairo => TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");

    public static TeacherThreadSlaPolicy WallClock(int replySlaHours = 24, int firstReminderAfterHours = 12, int secondReminderAfterHours = 20) => new(new SlaCalendar(false, [], TimeZoneInfo.Utc, []), TimeSpan.FromHours(replySlaHours), TimeSpan.FromHours(firstReminderAfterHours), TimeSpan.FromHours(secondReminderAfterHours));

    public static TeacherThreadSlaPolicy CairoWeekends(params SlaDateRange[] examPeriods) => new(new SlaCalendar(true, [DayOfWeek.Friday, DayOfWeek.Saturday], Cairo, examPeriods), TimeSpan.FromHours(24), TimeSpan.FromHours(12), TimeSpan.FromHours(20));
}
