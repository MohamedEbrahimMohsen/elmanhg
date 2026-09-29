using Elmanhg.Domain.Avatar;

namespace Elmanhg.Application.Avatar.Shared;

public static class AdminAvatarConversationResultGenerator
{
    public static AdminAvatarConversationResult Generate(AvatarConversation conversation, string studentName, string? subjectName, string? lessonName)
    {
        var firstQuestion = conversation.Messages
            .OrderBy(x => x.Position)
            .FirstOrDefault()?.Text ?? string.Empty;
        return new AdminAvatarConversationResult(conversation.Id, conversation.StudentId, studentName, conversation.EntryPoint, conversation.SubjectId, subjectName, conversation.LessonId, lessonName, conversation.QuestionId, conversation.StartedAt, conversation.LastMessageAt, conversation.MessageCount, firstQuestion);
    }

    public static AdminAvatarConversationDetailResult GenerateDetail(AvatarConversation conversation, string studentName, string? subjectName, string? unitName, string? lessonName)
    {
        var messages = conversation.Messages
            .OrderBy(x => x.Position)
            .Select(Message)
            .ToList();
        decimal? totalCostUsd = conversation.Messages.Any(x => x.CostUsd != null) ? conversation.Messages.Sum(x => x.CostUsd ?? 0m) : null;
        return new AdminAvatarConversationDetailResult(
            conversation.Id,
            conversation.StudentId,
            studentName,
            conversation.EntryPoint,
            conversation.SubjectId,
            subjectName,
            conversation.UnitId,
            unitName,
            conversation.LessonId,
            lessonName,
            conversation.SessionId,
            conversation.QuestionId,
            conversation.StartedAt,
            conversation.LastMessageAt,
            conversation.MessageCount,
            conversation.Messages.Sum(x => x.InputTokens ?? 0),
            conversation.Messages.Sum(x => x.OutputTokens ?? 0),
            totalCostUsd,
            messages);
    }

    private static AdminAvatarMessageResult Message(AvatarMessage message) => new(
        message.Id,
        message.Position,
        message.Role,
        message.Text,
        message.CreatedAt,
        message.Model,
        message.PromptVersion,
        message.InputTokens,
        message.OutputTokens,
        message.CostUsd,
        message.StopReason,
        message.HistoryMessageCount,
        message.Citations is null ? [] : AvatarMessageJson.ReadCitations(message.Citations),
        message.Context is null ? null : AvatarMessageJson.ReadContext(message.Context));
}
