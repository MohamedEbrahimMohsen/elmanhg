using Elmanhg.Application.Exceptions;
using Elmanhg.Application.SlaCalendars.DeleteExamPeriod;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.SlaCalendars.DeleteExamPeriod;

public sealed class DeleteExamPeriodValidatorTests
{
    private readonly DeleteExamPeriodValidator _validator = new();

    [Fact]
    public void Validate_Valid_Passes()
    {
        _validator.Validate(new DeleteExamPeriodCommand(Guid.NewGuid())).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyId_FailsIdRequired()
    {
        _validator.Validate(new DeleteExamPeriodCommand(Guid.Empty)).Errors.Select(x => x.ErrorCode).Should().Equal(ErrorCodes.ExamPeriodIdRequired);
    }
}
