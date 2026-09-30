using Core.DDD.Entities;

namespace Elmanhg.Domain.TeacherThreads;

public class TeacherThreadSlaEvent : Entity
{
    public Guid ThreadId { get; private set; }
    public TeacherThreadSlaEventKind Kind { get; private set; }
    public DateTimeOffset SlaDueAt { get; private set; }
    public Guid? TeacherId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    private TeacherThreadSlaEvent(Guid id) : base(id) { }

    public static TeacherThreadSlaEvent Record(Guid threadId, TeacherThreadSlaEventKind kind, DateTimeOffset slaDueAt, Guid? teacherId, DateTimeOffset occurredAt)
    {
        return new TeacherThreadSlaEvent(Guid.NewGuid())
        {
            ThreadId = threadId,
            Kind = kind,
            SlaDueAt = slaDueAt,
            TeacherId = teacherId,
            OccurredAt = TeacherThread.ToMicroseconds(occurredAt),
        };
    }
}
