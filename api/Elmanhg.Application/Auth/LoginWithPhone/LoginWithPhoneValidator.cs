using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Auth.LoginWithPhone;

public sealed class LoginWithPhoneValidator : AbstractValidator<LoginWithPhoneCommand>
{
    public LoginWithPhoneValidator()
    {
        RuleFor(x => x.VerificationId)
            .ValidateRequired(ErrorCodes.OtpVerificationIdInvalidFormat);
    }
}
