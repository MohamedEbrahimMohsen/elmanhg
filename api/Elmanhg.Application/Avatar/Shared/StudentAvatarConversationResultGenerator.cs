using Elmanhg.Domain.Avatar;

namespace Elmanhg.Application.Avatar.Shared;

public static class StudentAvatarConversationResultGenerator
{
    public static StudentAvatarConversationResult Generate(AvatarConversation conversation, string? subjectName, string? lessonName)
    {
        var firstQuestion = conversation.Messages
            .OrderBy(x => x.Position)
            .FirstOrDefault()?.Text ?? string.Empty;
        return new StudentAvatarConversationResult(conversation.Id, conversation.EntryPoint, conversation.SubjectId, subjectName, conversation.LessonId, lessonName, conversation.SessionId, conversation.QuestionId, conversation.StartedAt, conversation.LastMessageAt, conversation.MessageCount, firstQuestion);
    }

    public static StudentAvatarConversationDetailResult GenerateDetail(AvatarConversation conversation, string? subjectName, string? lessonName)
    {
        var messages = conversation.Messages
            .OrderBy(x => x.Position)
            .Select(Message)
            .ToList();
        return new StudentAvatarConversationDetailResult(conversation.Id, conversation.EntryPoint, conversation.SubjectId, subjectName, conversation.LessonId, lessonName, conversation.SessionId, conversation.QuestionId, conversation.StartedAt, conversation.LastMessageAt, conversation.MessageCount, messages);
    }

    private static StudentAvatarMessageResult Message(AvatarMessage message) => new(message.Id, message.Position, message.Role, message.Text, message.CreatedAt, message.Citations is null ? [] : AvatarMessageJson.ReadCitations(message.Citations));
}
