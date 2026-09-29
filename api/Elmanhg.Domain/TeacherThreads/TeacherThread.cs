using Core.DDD.Entities;

namespace Elmanhg.Domain.TeacherThreads;

public class TeacherThread : AuditEntity
{
    public Guid StudentId { get; private set; }
    public Guid SubjectId { get; private set; }
    public string Context { get; private set; } = "{}";
    public TeacherThreadStatus Status { get; private set; }
    public DateTimeOffset SubmittedAt { get; private set; }
    public DateTimeOffset SlaDueAt { get; private set; }
    public uint Version { get; private set; }
    public List<TeacherMessage> Messages { get; private set; } = [];

    private TeacherThread(Guid id, Guid? createdBy) : base(id, createdBy) { }

    public static TeacherThread Submit(Guid studentId, TeacherThreadContext context, string text, string? imageUrl, DateTimeOffset submittedAt, TimeSpan replySla)
    {
        var at = ToMicroseconds(submittedAt);
        var thread = new TeacherThread(Guid.NewGuid(), studentId)
        {
            StudentId = studentId,
            SubjectId = context.SubjectId,
            Context = context.ToJson(),
            Status = TeacherThreadStatus.Open,
            SubmittedAt = at,
            SlaDueAt = at + replySla,
        };
        thread.Messages.Add(TeacherMessage.CreateText(thread.Id, studentId, text, imageUrl, at));
        return thread;
    }

    public TeacherThreadContext ReadContext() => TeacherThreadContext.FromJson(Context);

    public bool IsOverdueAt(DateTimeOffset now) => Status == TeacherThreadStatus.Open && now >= SlaDueAt;

    // PostgreSQL timestamptz keeps microseconds; truncating keeps the returned result equal to what is stored.
    private static DateTimeOffset ToMicroseconds(DateTimeOffset value) => value.AddTicks(-(value.Ticks % TimeSpan.TicksPerMicrosecond));
}
