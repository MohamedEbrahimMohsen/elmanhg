using Core.DDD.Repositories;
using Core.Errors;
using Elmanhg.Application.Avatar.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.AiService;
using Elmanhg.Domain.Avatar;
using Microsoft.EntityFrameworkCore;

namespace Elmanhg.Application.Avatar.SendAvatarMessage;

public sealed partial class SendAvatarMessageHandler
{
    private async Task<AvatarConversation?> LoadConversationAsync(SendAvatarMessageCommand request, Guid studentId, CancellationToken cancellationToken)
    {
        if (request.ConversationId is not { } conversationId)
        {
            return null;
        }

        var conversation = await avatarConversationRepository.GetRequiredAsync(x => x.Id == conversationId && x.StudentId == studentId, ErrorCodes.AvatarConversationNotFound, cancellationToken, include: query => query.Include(x => x.Messages)).ConfigureAwait(false);
        if (!conversation.IsFor(request.EntryPoint, request.LessonId, request.SessionId, request.QuestionId))
        {
            throw new BadRequestCoreException(ErrorCodes.AvatarConversationContextMismatch);
        }

        return conversation;
    }

    private List<AiChatMessage> HistoryOf(AvatarConversation? conversation)
    {
        if (conversation is null)
        {
            return [];
        }

        return conversation.RecentMessages(avatarOptions.Value.MaxHistoryMessages)
            .Select(x => new AiChatMessage(x.Role == AvatarMessageRole.Student ? AiChatRole.User : AiChatRole.Assistant, AvatarText.Truncate(x.Text, avatarOptions.Value.HistoryTurnMaxLength)))
            .ToList();
    }

    private static AvatarConversation StartConversation(SendAvatarMessageCommand request, Guid studentId, AiContextBundle bundle, DateTimeOffset startedAt) => AvatarConversation.Start(studentId, request.EntryPoint, bundle.Subject?.Id, bundle.Unit?.Id, bundle.Lesson?.Id, request.EntryPoint is AvatarEntryPoint.QuizQuestion or AvatarEntryPoint.ExamReview ? request.SessionId : null, bundle.Question?.Id, startedAt);
}
