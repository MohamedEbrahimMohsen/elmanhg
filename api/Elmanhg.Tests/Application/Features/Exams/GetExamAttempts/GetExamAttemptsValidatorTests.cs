using Elmanhg.Application.Exams.GetExamAttempts;
using Elmanhg.Application.Exceptions;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Exams.GetExamAttempts;

public sealed class GetExamAttemptsValidatorTests
{
    private readonly GetExamAttemptsValidator _validator = new();

    [Fact]
    public void Validate_EmptySessionId_ReturnsSessionIdRequired()
    {
        _validator.Validate(new GetExamAttemptsQuery(Guid.Empty)).Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.SessionIdRequired);
    }

    [Fact]
    public void Validate_SessionId_IsValid()
    {
        _validator.Validate(new GetExamAttemptsQuery(Guid.NewGuid())).IsValid.Should().BeTrue();
    }
}
