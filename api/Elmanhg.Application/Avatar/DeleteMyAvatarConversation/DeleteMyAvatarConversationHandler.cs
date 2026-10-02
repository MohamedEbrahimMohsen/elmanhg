using Core.Errors;
using Core.Identity.Tokens.CurrentUser;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.RuntimeSettings;
using Elmanhg.Application.Shared.RuntimeSettings.Definitions;
using Elmanhg.Domain.Avatar;
using MediatR;

namespace Elmanhg.Application.Avatar.DeleteMyAvatarConversation;

public sealed class DeleteMyAvatarConversationHandler(IAvatarConversationRepository avatarConversationRepository, IRuntimeSettings runtimeSettings, TimeProvider timeProvider, ICurrentUserService currentUserService) : IRequestHandler<DeleteMyAvatarConversationCommand>
{
    public async Task Handle(DeleteMyAvatarConversationCommand request, CancellationToken cancellationToken)
    {
        if (currentUserService.UserId == null || currentUserService.UserId == default)
        {
            throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);
        }

        if (!await runtimeSettings.GetAsync(FeatureFlagRuntimeSettings.StudentsCanDeleteAvatarChats, cancellationToken).ConfigureAwait(false))
        {
            throw new BusinessRuleViolationCoreException(ErrorCodes.AvatarConversationDeletionDisabled);
        }

        var studentId = currentUserService.UserId.Value;
        var now = timeProvider.GetUtcNow();
        await avatarConversationRepository.ExecuteInTransactionAsync(async token =>
        {
            var conversation = await avatarConversationRepository.FirstOrDefaultAsync(x => x.Id == request.ConversationId && x.StudentId == studentId, token).ConfigureAwait(false) ?? throw new NotFoundCoreException(ErrorCodes.AvatarConversationNotFound);
            await avatarConversationRepository.EraseMessagesAsync(conversation.Id, token).ConfigureAwait(false);
            conversation.Delete(now);
            await avatarConversationRepository.SaveChangesAsync(token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }
}
