namespace Elmanhg.Domain.TeacherThreads;

public partial class TeacherThread
{
    public TeacherThreadSlaSchedule SlaSchedule() => new(SlaWindowStartedAt, FirstReminderDueAt, SecondReminderDueAt, SlaDueAt, SlaScheduleFingerprint);

    public List<TeacherThreadSlaEventKind> DueSlaStages(DateTimeOffset now)
    {
        if (Status != TeacherThreadStatus.Open)
        {
            return [];
        }

        var schedule = SlaSchedule();
        return Enum.GetValues<TeacherThreadSlaEventKind>()
            .Where(x => now >= schedule.DueAt(x))
            .ToList();
    }

    // Recomputes the stored stage times of the open window when the calendar, reply time or reminder hours changed; the window start never moves.
    public bool RescheduleSla(TeacherThreadSlaPolicy slaPolicy, DateTimeOffset rescheduledAt)
    {
        if (Status != TeacherThreadStatus.Open || SlaScheduleFingerprint == slaPolicy.Fingerprint)
        {
            return false;
        }

        ApplySlaSchedule(slaPolicy.ScheduleFrom(SlaWindowStartedAt));
        UpdationDate = ToMicroseconds(rescheduledAt);
        return true;
    }

    private void ApplySlaSchedule(TeacherThreadSlaSchedule schedule)
    {
        SlaWindowStartedAt = schedule.WindowStartedAt;
        FirstReminderDueAt = schedule.FirstReminderDueAt;
        SecondReminderDueAt = schedule.SecondReminderDueAt;
        SlaDueAt = schedule.SlaDueAt;
        SlaScheduleFingerprint = schedule.Fingerprint;
    }
}
