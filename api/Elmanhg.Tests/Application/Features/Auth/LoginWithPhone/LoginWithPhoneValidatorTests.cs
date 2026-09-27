using Elmanhg.Application.Auth.LoginWithPhone;
using Elmanhg.Application.Exceptions;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Auth.LoginWithPhone;

public sealed class LoginWithPhoneValidatorTests
{
    private readonly LoginWithPhoneValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        var result = _validator.Validate(new LoginWithPhoneCommand(Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyVerificationId_FailsWithVerificationIdInvalidFormat()
    {
        var result = _validator.Validate(new LoginWithPhoneCommand(Guid.Empty));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.OtpVerificationIdInvalidFormat);
    }
}
