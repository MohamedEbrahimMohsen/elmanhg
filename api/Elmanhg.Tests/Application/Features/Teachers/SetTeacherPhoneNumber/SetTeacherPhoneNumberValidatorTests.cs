using Core.OTP;
using Core.Validation;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Teachers.SetTeacherPhoneNumber;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.Teachers.SetTeacherPhoneNumber;

public sealed class SetTeacherPhoneNumberValidatorTests
{
    private readonly SetTeacherPhoneNumberValidator _validator = new(Options.Create(new OtpOptions { PhoneCodes = ["010", "011", "012", "015"], PhoneLength = 11 }));

    [Fact]
    public void Validate_ValidNumber_Passes()
    {
        var result = _validator.Validate(new SetTeacherPhoneNumberCommand(Guid.NewGuid(), "01512345678"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_NullNumber_Passes()
    {
        var result = _validator.Validate(new SetTeacherPhoneNumberCommand(Guid.NewGuid(), null));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyTeacherId_FailsWithTeacherIdRequired()
    {
        var result = _validator.Validate(new SetTeacherPhoneNumberCommand(Guid.Empty, "01012345678"));

        result.Errors.Select(x => x.ErrorCode).Should().Equal(ErrorCodes.TeacherIdRequired);
    }

    [Fact]
    public void Validate_WrongPrefix_FailsWithInvalidCellularCode()
    {
        var result = _validator.Validate(new SetTeacherPhoneNumberCommand(Guid.NewGuid(), "02012345678"));

        result.Errors.Select(x => x.ErrorCode).Should().Equal(ValidationErrors.ValidationPhoneNumberInvalidCellulerCode);
    }

    [Fact]
    public void Validate_TenDigits_FailsWithMustBeXDigits()
    {
        var result = _validator.Validate(new SetTeacherPhoneNumberCommand(Guid.NewGuid(), "0101234567"));

        result.Errors.Select(x => x.ErrorCode).Should().Equal(ValidationErrors.ValidationPhoneNumberMustBeXDigits);
    }

    [Fact]
    public void Validate_Letters_FailsWithOnlyDigits()
    {
        var result = _validator.Validate(new SetTeacherPhoneNumberCommand(Guid.NewGuid(), "0101234567a"));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ValidationErrors.ValidationPhoneNumberMustBeOnlyDigits);
    }

    [Fact]
    public void Validate_Empty_FailsWithRequired()
    {
        var result = _validator.Validate(new SetTeacherPhoneNumberCommand(Guid.NewGuid(), string.Empty));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ValidationErrors.ValidationPhoneNumberIsRequired);
    }
}
