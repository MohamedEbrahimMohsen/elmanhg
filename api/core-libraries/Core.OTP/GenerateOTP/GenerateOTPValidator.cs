using Core.OTP.Exceptions;
using Core.Validation.Extensions;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Core.OTP.GenerateOTP;

public sealed class GenerateOTPValidator : AbstractValidator<GenerateOTPCommand>
{

    public GenerateOTPValidator(IOptions<OtpOptions> options)
    {
        var otpOptions = options.Value;
        RuleFor(x => x)
            .Must(x => (x.PhoneNumber is null) != (x.Email is null))
            .WithErrorCode(ErrorCodes.OtpRecipientRequired);

        When(x => x.PhoneNumber is not null, () => RuleFor(x => x.PhoneNumber)
            .ValidatePhoneNumber(otpOptions.PhoneCodes, otpOptions.PhoneLength));

        When(x => x.Email is not null, () => RuleFor(x => x.Email)
            .ValidateRequired(ErrorCodes.EmailRequired)
            .ValidateEmail(ErrorCodes.EmailInvalid)
            .ValidateMaxLength(otpOptions.EmailMaxLength, ErrorCodes.EmailTooLong));
    }
}
