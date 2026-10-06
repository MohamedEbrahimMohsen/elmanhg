using Core.DDD.Entities;
using Core.Utilities.Time;

namespace Elmanhg.Domain.Avatar;

public class AvatarConversation : AuditEntity, IAuditedEntity
{
    public Guid StudentId { get; private set; }
    public AvatarEntryPoint EntryPoint { get; private set; }
    public Guid? SubjectId { get; private set; }
    public Guid? UnitId { get; private set; }
    public Guid? LessonId { get; private set; }
    public Guid? SessionId { get; private set; }
    public Guid? QuestionId { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset LastMessageAt { get; private set; }
    public int MessageCount { get; private set; }
    public uint Version { get; private set; }
    public List<AvatarMessage> Messages { get; private set; } = [];

    private AvatarConversation(Guid id, Guid? createdBy) : base(id, createdBy) { }

    public static AvatarConversation Start(Guid studentId, AvatarEntryPoint entryPoint, Guid? subjectId, Guid? unitId, Guid? lessonId, Guid? sessionId, Guid? questionId, DateTimeOffset startedAt)
    {
        var at = startedAt.TruncateToMicroseconds();
        return new AvatarConversation(Guid.NewGuid(), studentId)
        {
            StudentId = studentId,
            EntryPoint = entryPoint,
            SubjectId = subjectId,
            UnitId = unitId,
            LessonId = lessonId,
            SessionId = sessionId,
            QuestionId = questionId,
            StartedAt = at,
            LastMessageAt = at,
        };
    }

    public bool IsFor(AvatarEntryPoint entryPoint, Guid? lessonId, Guid? sessionId, Guid? questionId) => EntryPoint == entryPoint && entryPoint switch
    {
        AvatarEntryPoint.Lesson => LessonId == lessonId,
        AvatarEntryPoint.QuizQuestion or AvatarEntryPoint.ExamReview => SessionId == sessionId && QuestionId == questionId,
        _ => true,
    };

    public IReadOnlyList<AvatarMessage> RecentMessages(int count) => count <= 0 ? [] : Messages
        .OrderBy(x => x.Position)
        .TakeLast(count)
        .ToList();

    public void RecordExchange(string question, AvatarAssistantReply reply, DateTimeOffset askedAt, DateTimeOffset repliedAt)
    {
        var asked = askedAt.TruncateToMicroseconds();
        var replied = repliedAt.TruncateToMicroseconds();
        var studentMessage = AvatarMessage.FromStudent(Id, MessageCount, question.Trim(), asked);
        var assistantMessage = AvatarMessage.FromAssistant(Id, MessageCount + 1, reply, replied);
        Messages.Add(studentMessage);
        Messages.Add(assistantMessage);
        MessageCount += 2;
        LastMessageAt = replied;
        UpdatedBy = StudentId;
        UpdationDate = replied;
        RaiseDomainEvent(new AvatarExchangeRecorded(this, studentMessage, assistantMessage));
    }

    public void Delete(DateTimeOffset deletedAt)
    {
        var at = deletedAt.TruncateToMicroseconds();
        SoftDelete(at);
        MessageCount = 0;
        UpdatedBy = StudentId;
        UpdationDate = at;
    }
}
