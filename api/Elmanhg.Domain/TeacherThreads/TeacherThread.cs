using Core.DDD.Entities;
using Core.Utilities.Time;

namespace Elmanhg.Domain.TeacherThreads;

public partial class TeacherThread : AuditEntity, IVersioned
{
    public Guid StudentId { get; private set; }
    public Guid SubjectId { get; private set; }
    public string Context { get; private set; } = "{}";
    public TeacherThreadStatus Status { get; private set; }
    public DateTimeOffset SubmittedAt { get; private set; }
    public DateTimeOffset SlaWindowStartedAt { get; private set; }
    public DateTimeOffset FirstReminderDueAt { get; private set; }
    public DateTimeOffset SecondReminderDueAt { get; private set; }
    public DateTimeOffset SlaDueAt { get; private set; }
    public string SlaScheduleFingerprint { get; private set; } = string.Empty;
    public Guid? TeacherId { get; private set; }
    public DateTimeOffset? ClaimedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public int? Rating { get; private set; }
    public uint Version { get; private set; }
    public List<TeacherMessage> Messages { get; private set; } = [];

    private TeacherThread(Guid id, Guid? createdBy) : base(id, createdBy) { }

    public static TeacherThread Submit(Guid studentId, TeacherThreadContext context, string text, string? imageUrl, DateTimeOffset submittedAt, TeacherThreadSlaPolicy slaPolicy)
    {
        var at = submittedAt.TruncateToMicroseconds();
        var thread = new TeacherThread(Guid.NewGuid(), studentId)
        {
            StudentId = studentId,
            SubjectId = context.SubjectId,
            Context = context.ToJson(),
            Status = TeacherThreadStatus.Open,
            SubmittedAt = at,
        };
        thread.ApplySlaSchedule(slaPolicy.ScheduleFrom(at));
        thread.Messages.Add(TeacherMessage.CreateText(thread.Id, studentId, text, imageUrl, at));
        return thread;
    }

    public TeacherThreadContext ReadContext() => TeacherThreadContext.FromJson(Context);

    public bool IsOverdueAt(DateTimeOffset now) => Status == TeacherThreadStatus.Open && now >= SlaDueAt;

    public bool IsClaimedBy(Guid userId) => TeacherId == userId;

    public bool HasUnreadReply() => Messages.Any(x => x.SenderId != StudentId && x.StudentReadAt == null);

    // PostgreSQL timestamptz keeps microseconds; truncating keeps the returned result equal to what is stored.
    internal static DateTimeOffset ToMicroseconds(DateTimeOffset value) => value.TruncateToMicroseconds();
}
