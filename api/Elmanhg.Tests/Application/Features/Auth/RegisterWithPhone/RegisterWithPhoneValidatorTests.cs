using Elmanhg.Application.Auth.RegisterWithPhone;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.Auth.RegisterWithPhone;

public sealed class RegisterWithPhoneValidatorTests
{
    private readonly RegisterWithPhoneValidator _validator = new(Options.Create(new AuthOptions { DisplayNameMaxLength = 100 }));

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        var result = _validator.Validate(new RegisterWithPhoneCommand(Guid.NewGuid(), "Ahmed"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyVerificationId_FailsWithVerificationIdInvalidFormat()
    {
        var result = _validator.Validate(new RegisterWithPhoneCommand(Guid.Empty, "Ahmed"));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.OtpVerificationIdInvalidFormat);
    }

    [Fact]
    public void Validate_EmptyDisplayName_FailsWithDisplayNameRequired()
    {
        var result = _validator.Validate(new RegisterWithPhoneCommand(Guid.NewGuid(), string.Empty));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.DisplayNameRequired);
    }

    [Fact]
    public void Validate_DisplayNameOverMax_FailsWithDisplayNameTooLong()
    {
        var result = _validator.Validate(new RegisterWithPhoneCommand(Guid.NewGuid(), new string('a', 101)));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.DisplayNameTooLong);
    }
}
