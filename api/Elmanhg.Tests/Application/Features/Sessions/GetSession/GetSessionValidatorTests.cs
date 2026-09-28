using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Sessions.GetSession;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Sessions.GetSession;

public sealed class GetSessionValidatorTests
{
    private readonly GetSessionValidator _validator = new();

    [Fact]
    public void Validate_ValidQuery_Passes()
    {
        _validator.Validate(new GetSessionQuery(Guid.NewGuid())).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptySessionId_FailsSessionIdRequired()
    {
        _validator.Validate(new GetSessionQuery(Guid.Empty)).Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.SessionIdRequired);
    }
}
