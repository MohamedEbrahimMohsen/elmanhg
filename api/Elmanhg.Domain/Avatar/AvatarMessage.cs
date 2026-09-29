using Core.DDD.Entities;

namespace Elmanhg.Domain.Avatar;

public class AvatarMessage : Entity
{
    public Guid ConversationId { get; private set; }
    public int Position { get; private set; }
    public AvatarMessageRole Role { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public string? Model { get; private set; }
    public string? PromptVersion { get; private set; }
    public int? InputTokens { get; private set; }
    public int? OutputTokens { get; private set; }
    public decimal? CostUsd { get; private set; }
    public string? StopReason { get; private set; }
    public int? HistoryMessageCount { get; private set; }
    public string? Context { get; private set; }
    public string? Citations { get; private set; }

    private AvatarMessage(Guid id) : base(id) { }

    internal static AvatarMessage FromStudent(Guid conversationId, int position, string text, DateTimeOffset createdAt)
    {
        return new AvatarMessage(Guid.NewGuid())
        {
            ConversationId = conversationId,
            Position = position,
            Role = AvatarMessageRole.Student,
            Text = text,
            CreatedAt = createdAt,
        };
    }

    internal static AvatarMessage FromAssistant(Guid conversationId, int position, AvatarAssistantReply reply, DateTimeOffset createdAt)
    {
        return new AvatarMessage(Guid.NewGuid())
        {
            ConversationId = conversationId,
            Position = position,
            Role = AvatarMessageRole.Assistant,
            Text = reply.Text,
            CreatedAt = createdAt,
            Model = reply.Model,
            PromptVersion = reply.PromptVersion,
            InputTokens = reply.InputTokens,
            OutputTokens = reply.OutputTokens,
            CostUsd = reply.CostUsd,
            StopReason = reply.StopReason,
            HistoryMessageCount = reply.HistoryMessageCount,
            Context = reply.Context,
            Citations = reply.Citations,
        };
    }
}
