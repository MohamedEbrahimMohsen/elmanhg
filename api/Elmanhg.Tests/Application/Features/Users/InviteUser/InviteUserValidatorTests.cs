using Core.OTP;
using Core.Validation;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Users.InviteUser;
using Elmanhg.Domain.Identity;
using FluentAssertions;
using Microsoft.Extensions.Options;
using DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes;

namespace Elmanhg.Tests.Application.Features.Users.InviteUser;

public sealed class InviteUserValidatorTests
{
    private const string Email = "teacher@elmanhg.test";

    private readonly InviteUserValidator _validator = new(Options.Create(new AuthOptions { DisplayNameMaxLength = 100, EmailMaxLength = 256 }), Options.Create(new OtpOptions { PhoneCodes = ["010", "011", "012", "015"], PhoneLength = 11 }));

    [Fact]
    public void Validate_ValidTeacher_Passes()
    {
        var result = _validator.Validate(new InviteUserCommand(UserRole.Teacher, "Teacher", Email));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_StudentRole_FailsWithInviteRoleInvalid()
    {
        var result = _validator.Validate(new InviteUserCommand(UserRole.Student, "Student", Email));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.UserInviteRoleInvalid);
    }

    [Fact]
    public void Validate_EmptyDisplayName_FailsWithDisplayNameRequired()
    {
        var result = _validator.Validate(new InviteUserCommand(UserRole.Teacher, string.Empty, Email));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.DisplayNameRequired);
    }

    [Fact]
    public void Validate_LongDisplayName_FailsWithDisplayNameTooLong()
    {
        var result = _validator.Validate(new InviteUserCommand(UserRole.Teacher, new string('a', 101), Email));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.DisplayNameTooLong);
    }

    [Fact]
    public void Validate_EmptyEmail_FailsWithEmailRequired()
    {
        var result = _validator.Validate(new InviteUserCommand(UserRole.Admin, "Admin", string.Empty));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.EmailRequired);
    }

    [Fact]
    public void Validate_InvalidEmail_FailsWithEmailInvalid()
    {
        var result = _validator.Validate(new InviteUserCommand(UserRole.Admin, "Admin", "not-an-email"));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.EmailInvalid);
    }

    [Fact]
    public void Validate_LongEmail_FailsWithEmailTooLong()
    {
        var result = _validator.Validate(new InviteUserCommand(UserRole.Admin, "Admin", new string('a', 250) + "@elmanhg.test"));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.EmailTooLong);
    }

    [Fact]
    public void Validate_TeacherWithValidPhone_Passes()
    {
        var result = _validator.Validate(new InviteUserCommand(UserRole.Teacher, "Teacher", Email, "01512345678"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_TeacherWithInvalidPhone_FailsWithInvalidCellularCode()
    {
        var result = _validator.Validate(new InviteUserCommand(UserRole.Teacher, "Teacher", Email, "02012345678"));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ValidationErrors.ValidationPhoneNumberInvalidCellulerCode);
    }

    [Fact]
    public void Validate_AdminWithPhone_FailsWithPhoneNumberTeachersOnly()
    {
        var result = _validator.Validate(new InviteUserCommand(UserRole.Admin, "Admin", Email, "01012345678"));

        result.Errors.Select(x => x.ErrorCode).Should().Equal(DomainErrorCodes.PhoneNumberTeachersOnly);
    }
}
