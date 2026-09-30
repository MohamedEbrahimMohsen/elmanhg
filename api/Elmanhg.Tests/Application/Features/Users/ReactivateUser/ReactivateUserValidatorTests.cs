using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Users.ReactivateUser;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Users.ReactivateUser;

public sealed class ReactivateUserValidatorTests
{
    private readonly ReactivateUserValidator _validator = new();

    [Fact]
    public void Validate_UserId_Passes()
    {
        var result = _validator.Validate(new ReactivateUserCommand(Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyUserId_FailsWithUserIdRequired()
    {
        var result = _validator.Validate(new ReactivateUserCommand(Guid.Empty));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.UserIdRequired);
    }
}
