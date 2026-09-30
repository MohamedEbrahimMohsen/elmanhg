using Core.DDD.Entities;
using Elmanhg.Domain.Avatar;

namespace Elmanhg.Domain.TrainingData;

public class AvatarTrainingRecord : Entity
{
    public string StudentHash { get; private set; } = string.Empty;
    public Guid ConversationId { get; private set; }
    public Guid StudentMessageId { get; private set; }
    public Guid AssistantMessageId { get; private set; }
    public int StudentMessagePosition { get; private set; }
    public AvatarEntryPoint EntryPoint { get; private set; }
    public Guid? SubjectId { get; private set; }
    public Guid? UnitId { get; private set; }
    public Guid? LessonId { get; private set; }
    public Guid? QuestionId { get; private set; }
    public string StudentText { get; private set; } = string.Empty;
    public string AssistantText { get; private set; } = string.Empty;
    public string Model { get; private set; } = string.Empty;
    public string PromptVersion { get; private set; } = string.Empty;
    public string Context { get; private set; } = "{}";
    public DateTimeOffset AskedAt { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }

    private AvatarTrainingRecord(Guid id) : base(id) { }

    public static AvatarTrainingRecord From(AvatarConversation conversation, AvatarMessage studentMessage, AvatarMessage assistantMessage, string studentHash, DateTimeOffset recordedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(studentHash);
        if (studentMessage.Role != AvatarMessageRole.Student || assistantMessage.Role != AvatarMessageRole.Assistant)
        {
            throw new InvalidOperationException("An exchange is a student message followed by an assistant reply.");
        }

        if (studentMessage.ConversationId != conversation.Id || assistantMessage.ConversationId != conversation.Id)
        {
            throw new InvalidOperationException("Messages do not belong to the conversation.");
        }

        return new AvatarTrainingRecord(Guid.NewGuid())
        {
            StudentHash = studentHash,
            ConversationId = conversation.Id,
            StudentMessageId = studentMessage.Id,
            AssistantMessageId = assistantMessage.Id,
            StudentMessagePosition = studentMessage.Position,
            EntryPoint = conversation.EntryPoint,
            SubjectId = conversation.SubjectId,
            UnitId = conversation.UnitId,
            LessonId = conversation.LessonId,
            QuestionId = conversation.QuestionId,
            StudentText = studentMessage.Text,
            AssistantText = assistantMessage.Text,
            Model = assistantMessage.Model ?? throw MissingReplyMetadata(),
            PromptVersion = assistantMessage.PromptVersion ?? throw MissingReplyMetadata(),
            Context = assistantMessage.Context ?? throw MissingReplyMetadata(),
            AskedAt = studentMessage.CreatedAt,
            OccurredAt = assistantMessage.CreatedAt,
            RecordedAt = recordedAt,
        };
    }

    private static InvalidOperationException MissingReplyMetadata() => new("An assistant reply has no model, prompt version or context.");
}
