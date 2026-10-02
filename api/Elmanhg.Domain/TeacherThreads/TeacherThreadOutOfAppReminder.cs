using Core.DDD.Entities;

namespace Elmanhg.Domain.TeacherThreads;

public class TeacherThreadOutOfAppReminder : Entity
{
    public Guid ThreadId { get; private set; }
    public TeacherThreadSlaEventKind Stage { get; private set; }
    public DateTimeOffset SlaDueAt { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    private TeacherThreadOutOfAppReminder(Guid id) : base(id) { }

    public static TeacherThreadOutOfAppReminder Record(Guid threadId, TeacherThreadSlaEventKind stage, DateTimeOffset slaDueAt, DateTimeOffset occurredAt)
    {
        if (stage == TeacherThreadSlaEventKind.Breach)
        {
            throw new ArgumentOutOfRangeException(nameof(stage), stage, "The out-of-app reminder goes out at a reminder stage, never at the breach.");
        }

        return new TeacherThreadOutOfAppReminder(Guid.NewGuid())
        {
            ThreadId = threadId,
            Stage = stage,
            SlaDueAt = slaDueAt,
            OccurredAt = TeacherThread.ToMicroseconds(occurredAt),
        };
    }
}
