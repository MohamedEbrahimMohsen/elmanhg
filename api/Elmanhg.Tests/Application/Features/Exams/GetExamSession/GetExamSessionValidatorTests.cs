using Elmanhg.Application.Exams.GetExamSession;
using Elmanhg.Application.Exceptions;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Exams.GetExamSession;

public sealed class GetExamSessionValidatorTests
{
    private readonly GetExamSessionValidator _validator = new();

    [Fact]
    public void Validate_ValidId_Passes()
    {
        _validator.Validate(new GetExamSessionQuery(Guid.NewGuid())).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptySessionId_FailsWithSessionIdRequired()
    {
        _validator.Validate(new GetExamSessionQuery(Guid.Empty)).Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.SessionIdRequired);
    }
}
