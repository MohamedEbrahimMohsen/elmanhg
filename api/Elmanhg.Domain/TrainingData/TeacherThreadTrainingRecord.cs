using Core.DDD.Entities;
using Elmanhg.Domain.Questions.Schemas;
using Elmanhg.Domain.TeacherThreads;
using System.Text.Json;

namespace Elmanhg.Domain.TrainingData;

public class TeacherThreadTrainingRecord : Entity
{
    public string StudentHash { get; private set; } = string.Empty;
    public Guid ThreadId { get; private set; }
    public TeacherThreadTrainingTrigger Trigger { get; private set; }
    public Guid SubjectId { get; private set; }
    public Guid UnitId { get; private set; }
    public Guid LessonId { get; private set; }
    public Guid? QuestionId { get; private set; }
    public int? QuestionVersion { get; private set; }
    public Guid? AttemptId { get; private set; }
    public string Context { get; private set; } = "{}";
    public string Messages { get; private set; } = "[]";
    public int? Rating { get; private set; }
    public DateTimeOffset SubmittedAt { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }

    private TeacherThreadTrainingRecord(Guid id) : base(id) { }

    public IReadOnlyList<TeacherThreadTrainingMessage> ReadMessages() => JsonSerializer.Deserialize<List<TeacherThreadTrainingMessage>>(Messages, QuestionJson.SerializerOptions) ?? [];

    public static TeacherThreadTrainingRecord From(TeacherThread thread, TeacherThreadTrainingTrigger trigger, string studentHash, DateTimeOffset occurredAt, DateTimeOffset recordedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(studentHash);
        if (thread.Status != TeacherThreadStatus.Closed)
        {
            throw new InvalidOperationException("Only a closed thread becomes a training record.");
        }

        if (thread.Messages.Count == 0)
        {
            throw new InvalidOperationException("Thread messages must be loaded.");
        }

        var context = thread.ReadContext();
        var messages = thread.Messages
            .OrderBy(x => x.CreatedAt)
            .Select(x => new TeacherThreadTrainingMessage(x.SenderId == thread.StudentId ? TeacherThreadTrainingAuthor.Student : TeacherThreadTrainingAuthor.Teacher, x.Kind, x.Text, x.ImageUrl is not null, x.CreatedAt))
            .ToList();
        return new TeacherThreadTrainingRecord(Guid.NewGuid())
        {
            StudentHash = studentHash,
            ThreadId = thread.Id,
            Trigger = trigger,
            SubjectId = context.SubjectId,
            UnitId = context.UnitId,
            LessonId = context.LessonId,
            QuestionId = context.QuestionId,
            QuestionVersion = context.QuestionVersion,
            AttemptId = context.AttemptId,
            Context = thread.Context,
            Messages = JsonSerializer.Serialize(messages, QuestionJson.SerializerOptions),
            Rating = thread.Rating,
            SubmittedAt = thread.SubmittedAt,
            OccurredAt = occurredAt,
            RecordedAt = recordedAt,
        };
    }
}
