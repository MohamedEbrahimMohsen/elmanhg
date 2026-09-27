using Elmanhg.Application.Auth.RegisterWithEmail;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.Auth.RegisterWithEmail;

public sealed class RegisterWithEmailValidatorTests
{
    private const string DisplayName = "Mona";
    private const string Email = "mona@elmanhg.test";
    private const string Password = "Password1";

    private readonly RegisterWithEmailValidator _validator = new(Options.Create(new AuthOptions { DisplayNameMaxLength = 100, EmailMaxLength = 256 }), Options.Create(new IdentityOptions { Password = new PasswordOptions { RequiredLength = 8 } }));

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        var result = _validator.Validate(new RegisterWithEmailCommand(DisplayName, Email, Password));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyDisplayName_FailsWithDisplayNameRequired()
    {
        AssertFails(new RegisterWithEmailCommand(string.Empty, Email, Password), ErrorCodes.DisplayNameRequired);
    }

    [Fact]
    public void Validate_DisplayNameOverMax_FailsWithDisplayNameTooLong()
    {
        AssertFails(new RegisterWithEmailCommand(new string('a', 101), Email, Password), ErrorCodes.DisplayNameTooLong);
    }

    [Fact]
    public void Validate_EmptyEmail_FailsWithEmailRequired()
    {
        AssertFails(new RegisterWithEmailCommand(DisplayName, string.Empty, Password), ErrorCodes.EmailRequired);
    }

    [Fact]
    public void Validate_MalformedEmail_FailsWithEmailInvalid()
    {
        AssertFails(new RegisterWithEmailCommand(DisplayName, "not-an-email", Password), ErrorCodes.EmailInvalid);
    }

    [Fact]
    public void Validate_EmailOverMax_FailsWithEmailTooLong()
    {
        AssertFails(new RegisterWithEmailCommand(DisplayName, new string('a', 250) + "@elmanhg.test", Password), ErrorCodes.EmailTooLong);
    }

    [Fact]
    public void Validate_EmptyPassword_FailsWithPasswordRequired()
    {
        AssertFails(new RegisterWithEmailCommand(DisplayName, Email, string.Empty), ErrorCodes.PasswordIsRequired);
    }

    [Fact]
    public void Validate_ShortPassword_FailsWithPasswordTooShort()
    {
        AssertFails(new RegisterWithEmailCommand(DisplayName, Email, "Pass1"), ErrorCodes.PasswordTooShort);
    }

    [Fact]
    public void Validate_PasswordWithoutDigit_FailsWithPasswordMustContainDigit()
    {
        AssertFails(new RegisterWithEmailCommand(DisplayName, Email, "Password"), ErrorCodes.PasswordMustContainDigit);
    }

    private void AssertFails(RegisterWithEmailCommand command, string errorCode)
    {
        _validator.Validate(command).Errors.Select(x => x.ErrorCode).Should().Contain(errorCode);
    }
}
