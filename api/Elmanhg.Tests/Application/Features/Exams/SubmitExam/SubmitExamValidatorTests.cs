using Elmanhg.Application.Exams.SubmitExam;
using Elmanhg.Application.Exceptions;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Exams.SubmitExam;

public sealed class SubmitExamValidatorTests
{
    private readonly SubmitExamValidator _validator = new();

    [Fact]
    public void Validate_ValidId_Passes()
    {
        _validator.Validate(new SubmitExamCommand(Guid.NewGuid())).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptySessionId_FailsWithSessionIdRequired()
    {
        _validator.Validate(new SubmitExamCommand(Guid.Empty)).Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.SessionIdRequired);
    }
}
