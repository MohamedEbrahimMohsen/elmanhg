using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Auth.AcceptInvitation;

public sealed class AcceptInvitationValidator : AbstractValidator<AcceptInvitationCommand>
{
    public AcceptInvitationValidator(IOptions<IdentityOptions> identityOptions)
    {
        var identity = identityOptions.Value;

        RuleFor(x => x.VerificationId).ValidateRequired(ErrorCodes.OtpVerificationIdInvalidFormat);

        RuleFor(x => x.Password)
            .ValidateRequired(ErrorCodes.PasswordIsRequired)
            .ValidateMinLength(identity.Password.RequiredLength, ErrorCodes.PasswordTooShort)
            .ValidateHasNumber(ErrorCodes.PasswordMustContainDigit);
    }
}
