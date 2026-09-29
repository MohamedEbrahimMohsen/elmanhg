namespace Elmanhg.Domain.TeacherThreads;

public partial class TeacherThread
{
    public List<TeacherThreadSlaEventKind> DueSlaStages(DateTimeOffset now, TimeSpan replySla, TimeSpan firstReminderAfter, TimeSpan secondReminderAfter)
    {
        if (Status != TeacherThreadStatus.Open)
        {
            return [];
        }

        var windowStart = SlaDueAt - replySla;
        List<TeacherThreadSlaEventKind> stages = [];
        if (now >= windowStart + firstReminderAfter)
        {
            stages.Add(TeacherThreadSlaEventKind.FirstReminder);
        }

        if (now >= windowStart + secondReminderAfter)
        {
            stages.Add(TeacherThreadSlaEventKind.SecondReminder);
        }

        if (now >= SlaDueAt)
        {
            stages.Add(TeacherThreadSlaEventKind.Breach);
        }

        return stages;
    }
}
