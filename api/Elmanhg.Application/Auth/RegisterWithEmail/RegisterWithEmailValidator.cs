using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Identity;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Application.Auth.RegisterWithEmail;

public sealed class RegisterWithEmailValidator : AbstractValidator<RegisterWithEmailCommand>
{
    public RegisterWithEmailValidator(IOptions<AuthOptions> authOptions, IOptions<IdentityOptions> identityOptions)
    {
        var options = authOptions.Value;
        var identity = identityOptions.Value;

        RuleFor(x => x.DisplayName)
            .ValidateRequired(ErrorCodes.DisplayNameRequired)
            .ValidateMaxLength(options.DisplayNameMaxLength, ErrorCodes.DisplayNameTooLong);

        RuleFor(x => x.Email)
            .ValidateRequired(ErrorCodes.EmailRequired)
            .ValidateEmail(ErrorCodes.EmailInvalid)
            .ValidateMaxLength(options.EmailMaxLength, ErrorCodes.EmailTooLong);

        RuleFor(x => x.Password)
            .ValidateRequired(ErrorCodes.PasswordIsRequired)
            .ValidateMinLength(identity.Password.RequiredLength, ErrorCodes.PasswordTooShort)
            .ValidateHasNumber(ErrorCodes.PasswordMustContainDigit);

        RuleFor(x => x.TermsVersion)
            .Cascade(CascadeMode.Stop)
            .ValidateRequired(ErrorCodes.TermsVersionRequired)
            .Must(TermsVersions.IsKnown)
            .WithErrorCode(DomainErrorCodes.TermsVersionUnknown);
    }
}
