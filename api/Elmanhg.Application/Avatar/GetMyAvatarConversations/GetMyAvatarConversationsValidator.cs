using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Avatar.GetMyAvatarConversations;

public sealed class GetMyAvatarConversationsValidator : AbstractValidator<GetMyAvatarConversationsQuery>
{
    public GetMyAvatarConversationsValidator(IOptions<AvatarOptions> avatarOptions)
    {
        var options = avatarOptions.Value;

        RuleFor(x => x.PageNumber).ValidateMin(1, ErrorCodes.AvatarConversationsPageNumberInvalid);
        RuleFor(x => x.PageSize).ValidateRange(1, options.StudentConversationsMaxPageSize, ErrorCodes.AvatarConversationsPageSizeInvalid);
    }
}
