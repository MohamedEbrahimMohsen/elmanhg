using Elmanhg.Application.Auth.LoginWithEmail;
using Elmanhg.Application.Exceptions;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Auth.LoginWithEmail;

public sealed class LoginWithEmailValidatorTests
{
    private readonly LoginWithEmailValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        var result = _validator.Validate(new LoginWithEmailCommand("admin@elmanhg.test", "Password1"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyEmail_FailsWithEmailRequired()
    {
        var result = _validator.Validate(new LoginWithEmailCommand(string.Empty, "Password1"));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.EmailRequired);
    }

    [Fact]
    public void Validate_MalformedEmail_FailsWithEmailInvalid()
    {
        var result = _validator.Validate(new LoginWithEmailCommand("not-an-email", "Password1"));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.EmailInvalid);
    }

    [Fact]
    public void Validate_EmptyPassword_FailsWithPasswordRequired()
    {
        var result = _validator.Validate(new LoginWithEmailCommand("admin@elmanhg.test", string.Empty));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.PasswordIsRequired);
    }
}
