using System.ComponentModel.DataAnnotations;

namespace Elmanhg.Application.Shared.Options;

public sealed class AvatarOptions
{
    public const string SectionName = "Avatar";

    // Upper bounds are the AI service limits: chat_max_message_chars 4000 and chat_max_history_messages 20.
    [Range(1, 4000)]
    public int MessageMaxLength { get; set; } = 2000;

    // Each stored turn is cut to this when sent back as history; the AI service rejects turns over 4000.
    [Range(1, 4000)]
    public int HistoryTurnMaxLength { get; set; } = 4000;

    [Range(0, 20)]
    public int MaxHistoryMessages { get; set; } = 10;

    // Six fields of 8000 plus the objectives stay under the AI service's 60 000-character context limit.
    [Range(500, 8000)]
    public int ContextFieldMaxLength { get; set; } = 8000;

    [Range(1, 200)]
    public int AdminConversationsMaxPageSize { get; set; } = 100;

    [Range(1, 500)]
    public int ConversationSearchMaxLength { get; set; } = 200;
}
