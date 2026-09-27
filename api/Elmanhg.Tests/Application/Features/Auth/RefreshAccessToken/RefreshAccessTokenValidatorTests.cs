using Elmanhg.Application.Auth.RefreshAccessToken;
using Elmanhg.Application.Exceptions;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Auth.RefreshAccessToken;

public sealed class RefreshAccessTokenValidatorTests
{
    private readonly RefreshAccessTokenValidator _validator = new();

    [Fact]
    public void Validate_Token_Passes()
    {
        var result = _validator.Validate(new RefreshAccessTokenCommand("refresh-token"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyToken_FailsWithRefreshTokenRequired()
    {
        var result = _validator.Validate(new RefreshAccessTokenCommand(string.Empty));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.RefreshTokenIsRequired);
    }
}
