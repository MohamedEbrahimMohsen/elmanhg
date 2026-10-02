using Core.OTP;
using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Teachers.SetTeacherPhoneNumber;

public sealed class SetTeacherPhoneNumberValidator : AbstractValidator<SetTeacherPhoneNumberCommand>
{
    public SetTeacherPhoneNumberValidator(IOptions<OtpOptions> otpOptions)
    {
        var otp = otpOptions.Value;

        RuleFor(x => x.TeacherId).ValidateRequired(ErrorCodes.TeacherIdRequired);

        When(x => x.PhoneNumber is not null, () => RuleFor(x => x.PhoneNumber).ValidatePhoneNumber(otp.PhoneCodes, otp.PhoneLength));
    }
}
