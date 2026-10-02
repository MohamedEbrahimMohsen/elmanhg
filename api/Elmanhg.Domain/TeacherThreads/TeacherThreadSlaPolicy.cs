using Elmanhg.Domain.SlaCalendars;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Elmanhg.Domain.TeacherThreads;

public sealed class TeacherThreadSlaPolicy
{
    public TeacherThreadSlaPolicy(SlaCalendar calendar, TimeSpan replySla, TimeSpan firstReminderAfter, TimeSpan secondReminderAfter)
    {
        Calendar = calendar;
        ReplySla = replySla;
        FirstReminderAfter = firstReminderAfter;
        SecondReminderAfter = secondReminderAfter;
        Fingerprint = ComputeFingerprint();
    }

    public SlaCalendar Calendar { get; }
    public TimeSpan ReplySla { get; }
    public TimeSpan FirstReminderAfter { get; }
    public TimeSpan SecondReminderAfter { get; }
    public string Fingerprint { get; }

    public TeacherThreadSlaSchedule ScheduleFrom(DateTimeOffset windowStartedAt) => new(windowStartedAt, Calendar.AddCountedTime(windowStartedAt, FirstReminderAfter), Calendar.AddCountedTime(windowStartedAt, SecondReminderAfter), Calendar.AddCountedTime(windowStartedAt, ReplySla), Fingerprint);

    private string ComputeFingerprint()
    {
        var weekendDays = string.Join(',', Calendar.WeekendDays.Select(x => (int)x).Order());
        var examPeriods = string.Join(';', Calendar.ExamPeriods
            .OrderBy(x => x.Start)
            .ThenBy(x => x.End)
            .Select(x => string.Create(CultureInfo.InvariantCulture, $"{x.Start:yyyy-MM-dd}..{x.End:yyyy-MM-dd}")));
        var canonical = string.Create(CultureInfo.InvariantCulture, $"{ReplySla.Ticks}|{FirstReminderAfter.Ticks}|{SecondReminderAfter.Ticks}|{Calendar.SkipWeekends}|{weekendDays}|{Calendar.TimeZone.Id}|{examPeriods}");
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
