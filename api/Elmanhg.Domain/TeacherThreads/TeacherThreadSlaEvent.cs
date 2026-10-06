using Core.DDD.Entities;
using Core.Utilities.Time;

namespace Elmanhg.Domain.TeacherThreads;

public class TeacherThreadSlaEvent : Entity
{
    public Guid ThreadId { get; private set; }
    public TeacherThreadSlaEventKind Kind { get; private set; }
    public DateTimeOffset WindowStartedAt { get; private set; }
    public DateTimeOffset SlaDueAt { get; private set; }
    public Guid? TeacherId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    private TeacherThreadSlaEvent(Guid id) : base(id) { }

    public static TeacherThreadSlaEvent Record(Guid threadId, TeacherThreadSlaEventKind kind, DateTimeOffset windowStartedAt, DateTimeOffset slaDueAt, Guid? teacherId, DateTimeOffset occurredAt)
    {
        return new TeacherThreadSlaEvent(Guid.NewGuid())
        {
            ThreadId = threadId,
            Kind = kind,
            WindowStartedAt = windowStartedAt,
            SlaDueAt = slaDueAt,
            TeacherId = teacherId,
            OccurredAt = occurredAt.TruncateToMicroseconds(),
        };
    }
}
