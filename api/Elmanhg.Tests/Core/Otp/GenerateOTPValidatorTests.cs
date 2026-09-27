using Core.OTP;
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
}
