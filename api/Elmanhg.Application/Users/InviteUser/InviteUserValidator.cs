using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Identity;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Users.InviteUser;

public sealed class InviteUserValidator : AbstractValidator<InviteUserCommand>
{
    public InviteUserValidator(IOptions<AuthOptions> authOptions)
    {
        var options = authOptions.Value;

        RuleFor(x => x.Role)
            .Must(x => x is UserRole.Teacher or UserRole.Admin)
            .WithErrorCode(ErrorCodes.UserInviteRoleInvalid);

        RuleFor(x => x.DisplayName)
            .ValidateRequired(ErrorCodes.DisplayNameRequired)
            .ValidateMaxLength(options.DisplayNameMaxLength, ErrorCodes.DisplayNameTooLong);

        RuleFor(x => x.Email)
            .ValidateRequired(ErrorCodes.EmailRequired)
            .ValidateEmail(ErrorCodes.EmailInvalid)
            .ValidateMaxLength(options.EmailMaxLength, ErrorCodes.EmailTooLong);
    }
}
