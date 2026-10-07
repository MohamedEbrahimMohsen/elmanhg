using Core.DDD.Repositories;
using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Core.Settings;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.RuntimeSettings.Definitions;
using Elmanhg.Domain.Avatar;
using MediatR;

namespace Elmanhg.Application.Avatar.DeleteMyAvatarConversation;

public sealed class DeleteMyAvatarConversationHandler(IAvatarConversationRepository avatarConversationRepository, IRuntimeSettings runtimeSettings, TimeProvider timeProvider, ICurrentUserService currentUserService) : IRequestHandler<DeleteMyAvatarConversationCommand>
{
    public async Task Handle(DeleteMyAvatarConversationCommand request, CancellationToken cancellationToken)
    {
        var studentId = currentUserService.GetRequiredUserId(ErrorCodes.UserNotAuthenticated);

        if (!await runtimeSettings.GetAsync(FeatureFlagRuntimeSettings.StudentsCanDeleteAvatarChats, cancellationToken).ConfigureAwait(false))
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.AvatarConversationDeletionDisabled);
        }

        var now = timeProvider.GetUtcNow();
        await avatarConversationRepository.ExecuteInTransactionAsync(async token =>
        {
            var conversation = await avatarConversationRepository.GetRequiredAsync(x => x.Id == request.ConversationId && x.StudentId == studentId, ErrorCodes.AvatarConversationNotFound, token).ConfigureAwait(false);
            await avatarConversationRepository.EraseMessagesAsync(conversation.Id, token).ConfigureAwait(false);
            conversation.Delete(now);
            await avatarConversationRepository.SaveChangesAsync(token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }
}
