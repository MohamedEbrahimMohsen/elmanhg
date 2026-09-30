using Elmanhg.Application.Auth.AcceptInvitation;
using Elmanhg.Application.Exceptions;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.Auth.AcceptInvitation;

public sealed class AcceptInvitationValidatorTests
{
    private const string Password = "Password1";

    private readonly AcceptInvitationValidator _validator = new(Options.Create(new IdentityOptions { Password = new PasswordOptions { RequiredLength = 8 } }));

    [Fact]
    public void Validate_Valid_Passes()
    {
        var result = _validator.Validate(new AcceptInvitationCommand(Guid.NewGuid(), Password));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyVerificationId_FailsWithInvalidFormat()
    {
        AssertFails(new AcceptInvitationCommand(Guid.Empty, Password), ErrorCodes.OtpVerificationIdInvalidFormat);
    }

    [Fact]
    public void Validate_EmptyPassword_FailsWithPasswordRequired()
    {
        AssertFails(new AcceptInvitationCommand(Guid.NewGuid(), string.Empty), ErrorCodes.PasswordIsRequired);
    }

    [Fact]
    public void Validate_ShortPassword_FailsWithPasswordTooShort()
    {
        AssertFails(new AcceptInvitationCommand(Guid.NewGuid(), "Pass1"), ErrorCodes.PasswordTooShort);
    }

    [Fact]
    public void Validate_PasswordWithoutDigit_FailsWithMustContainDigit()
    {
        AssertFails(new AcceptInvitationCommand(Guid.NewGuid(), "Passwords"), ErrorCodes.PasswordMustContainDigit);
    }

    private void AssertFails(AcceptInvitationCommand command, string errorCode)
    {
        _validator.Validate(command).Errors.Select(x => x.ErrorCode).Should().Contain(errorCode);
    }
}
