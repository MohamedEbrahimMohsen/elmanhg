namespace Elmanhg.Domain.TeacherThreads;

public sealed record TeacherThreadSlaSchedule(DateTimeOffset WindowStartedAt, DateTimeOffset FirstReminderDueAt, DateTimeOffset SecondReminderDueAt, DateTimeOffset SlaDueAt, string Fingerprint)
{
    public DateTimeOffset DueAt(TeacherThreadSlaEventKind kind) => kind switch
    {
        TeacherThreadSlaEventKind.FirstReminder => FirstReminderDueAt,
        TeacherThreadSlaEventKind.SecondReminder => SecondReminderDueAt,
        TeacherThreadSlaEventKind.Breach => SlaDueAt,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
