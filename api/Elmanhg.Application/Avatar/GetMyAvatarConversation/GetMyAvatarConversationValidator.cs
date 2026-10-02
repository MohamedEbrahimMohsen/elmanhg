using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Avatar.GetMyAvatarConversation;

public sealed class GetMyAvatarConversationValidator : AbstractValidator<GetMyAvatarConversationQuery>
{
    public GetMyAvatarConversationValidator()
    {
        RuleFor(x => x.ConversationId).ValidateRequired(ErrorCodes.AvatarConversationIdRequired);
    }
}
