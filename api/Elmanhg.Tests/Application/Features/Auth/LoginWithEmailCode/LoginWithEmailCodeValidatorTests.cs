using Elmanhg.Application.Auth.LoginWithEmailCode;
using Elmanhg.Application.Exceptions;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Auth.LoginWithEmailCode;

public sealed class LoginWithEmailCodeValidatorTests
{
    private readonly LoginWithEmailCodeValidator _validator = new();

    [Fact]
    public void Validate_VerificationId_Passes()
    {
        var result = _validator.Validate(new LoginWithEmailCodeCommand(Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyVerificationId_FailsWithFormatCode()
    {
        var result = _validator.Validate(new LoginWithEmailCodeCommand(Guid.Empty));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.OtpVerificationIdInvalidFormat);
    }
}
