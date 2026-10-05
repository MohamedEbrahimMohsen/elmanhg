using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Identity;
using FluentValidation;
using Microsoft.Extensions.Options;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Application.Auth.RegisterWithPhone;

public sealed class RegisterWithPhoneValidator : AbstractValidator<RegisterWithPhoneCommand>
{
    public RegisterWithPhoneValidator(IOptions<AuthOptions> authOptions)
    {
        var options = authOptions.Value;

        RuleFor(x => x.VerificationId)
            .ValidateRequired(ErrorCodes.OtpVerificationIdInvalidFormat);

        RuleFor(x => x.DisplayName)
            .ValidateRequired(ErrorCodes.DisplayNameRequired)
            .ValidateMaxLength(options.DisplayNameMaxLength, ErrorCodes.DisplayNameTooLong);

        RuleFor(x => x.TermsVersion)
            .Cascade(CascadeMode.Stop)
            .ValidateRequired(ErrorCodes.TermsVersionRequired)
            .Must(TermsVersions.IsKnown)
            .WithErrorCode(DomainErrorCodes.TermsVersionUnknown);
    }
}
