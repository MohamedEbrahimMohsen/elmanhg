using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

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
    }
}
