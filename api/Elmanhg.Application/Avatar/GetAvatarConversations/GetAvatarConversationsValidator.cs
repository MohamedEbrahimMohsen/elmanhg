using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Avatar.GetAvatarConversations;

public sealed class GetAvatarConversationsValidator : AbstractValidator<GetAvatarConversationsQuery>
{
    public GetAvatarConversationsValidator(IOptions<AvatarOptions> avatarOptions)
    {
        var options = avatarOptions.Value;

        RuleFor(x => x).ValidatePaging(x => x.PageNumber, x => x.PageSize, options.AdminConversationsMaxPageSize, ErrorCodes.AvatarConversationsPageNumberInvalid, ErrorCodes.AvatarConversationsPageSizeInvalid);
        RuleFor(x => x.Search).ValidateMaxLength(options.ConversationSearchMaxLength, ErrorCodes.AvatarConversationsSearchTooLong);
        RuleFor(x => x.EntryPoint)
            .IsInEnum()
            .WithErrorCode(ErrorCodes.AvatarConversationsEntryPointInvalid);
        RuleFor(x => x).ValidateDateRange(x => x.From, x => x.To, ErrorCodes.AvatarConversationsDateRangeInvalid);
    }
}
