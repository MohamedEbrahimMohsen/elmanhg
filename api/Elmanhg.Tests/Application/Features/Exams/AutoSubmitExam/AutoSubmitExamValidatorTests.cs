using Elmanhg.Application.Exams.AutoSubmitExam;
using Elmanhg.Application.Exceptions;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Exams.AutoSubmitExam;

public sealed class AutoSubmitExamValidatorTests
{
    private readonly AutoSubmitExamValidator _validator = new();

    [Fact]
    public void Validate_ValidId_Passes()
    {
        _validator.Validate(new AutoSubmitExamCommand(Guid.NewGuid())).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptySessionId_FailsWithSessionIdRequired()
    {
        _validator.Validate(new AutoSubmitExamCommand(Guid.Empty)).Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.SessionIdRequired);
    }
}
