using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Avatar.DeleteMyAvatarConversation;

public sealed class DeleteMyAvatarConversationValidator : AbstractValidator<DeleteMyAvatarConversationCommand>
{
    public DeleteMyAvatarConversationValidator()
    {
        RuleFor(x => x.ConversationId).ValidateRequired(ErrorCodes.AvatarConversationIdRequired);
    }
}
