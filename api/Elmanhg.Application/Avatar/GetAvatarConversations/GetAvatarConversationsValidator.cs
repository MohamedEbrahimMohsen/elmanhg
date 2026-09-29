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

        RuleFor(x => x.PageNumber).ValidateMin(1, ErrorCodes.AvatarConversationsPageNumberInvalid);
        RuleFor(x => x.PageSize).ValidateRange(1, options.AdminConversationsMaxPageSize, ErrorCodes.AvatarConversationsPageSizeInvalid);
        RuleFor(x => x.Search).ValidateMaxLength(options.ConversationSearchMaxLength, ErrorCodes.AvatarConversationsSearchTooLong);
        RuleFor(x => x.EntryPoint)
            .IsInEnum()
            .WithErrorCode(ErrorCodes.AvatarConversationsEntryPointInvalid);
        RuleFor(x => x).Must(x => x.From is null || x.To is null || x.From < x.To).WithErrorCode(ErrorCodes.AvatarConversationsDateRangeInvalid);
    }
}
