using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Users.SuspendUser;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Users.SuspendUser;

public sealed class SuspendUserValidatorTests
{
    private readonly SuspendUserValidator _validator = new();

    [Fact]
    public void Validate_UserId_Passes()
    {
        var result = _validator.Validate(new SuspendUserCommand(Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyUserId_FailsWithUserIdRequired()
    {
        var result = _validator.Validate(new SuspendUserCommand(Guid.Empty));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.UserIdRequired);
    }
}
