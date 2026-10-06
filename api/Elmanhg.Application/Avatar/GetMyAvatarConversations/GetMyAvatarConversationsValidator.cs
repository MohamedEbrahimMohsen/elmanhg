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

        RuleFor(x => x).ValidatePaging(x => x.PageNumber, x => x.PageSize, options.StudentConversationsMaxPageSize, ErrorCodes.AvatarConversationsPageNumberInvalid, ErrorCodes.AvatarConversationsPageSizeInvalid);
    }
}
