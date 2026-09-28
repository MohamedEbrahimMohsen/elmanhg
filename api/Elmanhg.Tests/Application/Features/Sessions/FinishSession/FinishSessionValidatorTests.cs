using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Sessions.FinishSession;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Sessions.FinishSession;

public sealed class FinishSessionValidatorTests
{
    private readonly FinishSessionValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        _validator.Validate(new FinishSessionCommand(Guid.NewGuid())).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptySessionId_FailsSessionIdRequired()
    {
        _validator.Validate(new FinishSessionCommand(Guid.Empty)).Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.SessionIdRequired);
    }
}
