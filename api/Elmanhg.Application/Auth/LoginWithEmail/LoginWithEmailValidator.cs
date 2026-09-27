using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Auth.LoginWithEmail;

public sealed class LoginWithEmailValidator : AbstractValidator<LoginWithEmailCommand>
{
    public LoginWithEmailValidator()
    {
        RuleFor(x => x.Email)
            .ValidateRequired(ErrorCodes.EmailRequired)
            .ValidateEmail(ErrorCodes.EmailInvalid);

        RuleFor(x => x.Password)
            .ValidateRequired(ErrorCodes.PasswordIsRequired);
    }
}
