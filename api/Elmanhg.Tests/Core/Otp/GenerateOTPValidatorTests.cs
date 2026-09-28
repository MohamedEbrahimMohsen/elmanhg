using Core.OTP;
using Core.OTP.Exceptions;
using Core.OTP.GenerateOTP;
using Core.Validation;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Core.Otp;

public sealed class GenerateOTPValidatorTests
{
    private readonly GenerateOTPValidator _validator = new(Options.Create(new OtpOptions { PhoneCodes = ["010", "011", "012", "015"], PhoneLength = 11 }));

    [Fact]
    public void Validate_ValidMobileNumber_Passes()
    {
        var result = _validator.Validate(new GenerateOTPCommand("01512345678"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_Empty_FailsWithPhoneRequired()
    {
        var result = _validator.Validate(new GenerateOTPCommand(string.Empty));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ValidationErrors.ValidationPhoneNumberIsRequired);
    }

    [Fact]
    public void Validate_NonDigits_FailsWithOnlyDigits()
    {
        var result = _validator.Validate(new GenerateOTPCommand("0101234567a"));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ValidationErrors.ValidationPhoneNumberMustBeOnlyDigits);
    }

    [Fact]
    public void Validate_WrongLength_FailsWithMustBeXDigits()
    {
        var result = _validator.Validate(new GenerateOTPCommand("0101234567"));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ValidationErrors.ValidationPhoneNumberMustBeXDigits);
    }

    [Fact]
    public void Validate_UnknownPrefix_FailsWithInvalidCellularCode()
    {
        var result = _validator.Validate(new GenerateOTPCommand("01312345678"));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ValidationErrors.ValidationPhoneNumberInvalidCellulerCode);
    }

    [Fact]
    public void Validate_ValidEmail_Passes()
    {
        var result = _validator.Validate(new GenerateOTPCommand(null, "mona@elmanhg.test"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_NeitherPhoneNorEmail_FailsWithRecipientRequired()
    {
        var result = _validator.Validate(new GenerateOTPCommand(null, null));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.OtpRecipientRequired);
    }

    [Fact]
    public void Validate_BothPhoneAndEmail_FailsWithRecipientRequired()
    {
        var result = _validator.Validate(new GenerateOTPCommand("01012345678", "a@b.com"));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.OtpRecipientRequired);
    }

    [Fact]
    public void Validate_EmptyEmail_FailsWithEmailRequired()
    {
        var result = _validator.Validate(new GenerateOTPCommand(null, string.Empty));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.EmailRequired);
    }

    [Fact]
    public void Validate_MalformedEmail_FailsWithEmailInvalid()
    {
        var result = _validator.Validate(new GenerateOTPCommand(null, "mona"));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.EmailInvalid);
    }

    [Fact]
    public void Validate_EmailTooLong_FailsWithEmailTooLong()
    {
        var validator = new GenerateOTPValidator(Options.Create(new OtpOptions { EmailMaxLength = 20 }));

        var result = validator.Validate(new GenerateOTPCommand(null, "mona.ahmed@elmanhg-school.test"));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.EmailTooLong);
    }
}
