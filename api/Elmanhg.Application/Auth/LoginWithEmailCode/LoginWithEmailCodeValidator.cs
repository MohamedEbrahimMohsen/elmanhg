using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Auth.LoginWithEmailCode;

public sealed class LoginWithEmailCodeValidator : AbstractValidator<LoginWithEmailCodeCommand>
{
    public LoginWithEmailCodeValidator()
    {
        RuleFor(x => x.VerificationId)
            .ValidateRequired(ErrorCodes.OtpVerificationIdInvalidFormat);
    }
}
