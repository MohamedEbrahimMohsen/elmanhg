using Elmanhg.Application.Exceptions;
using Elmanhg.Application.MathStepGrading.GetMathStepGrade;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.MathStepGrading.GetMathStepGrade;

public sealed class GetMathStepGradeValidatorTests
{
    private readonly GetMathStepGradeValidator _validator = new();

    [Fact]
    public void Validate_Valid_Passes()
    {
        _validator.Validate(new GetMathStepGradeQuery(Guid.NewGuid(), Guid.NewGuid())).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptySessionId_ReturnsSessionIdRequired()
    {
        _validator.Validate(new GetMathStepGradeQuery(Guid.Empty, Guid.NewGuid())).Errors.Select(x => x.ErrorCode).Should().Equal(ErrorCodes.SessionIdRequired);
    }

    [Fact]
    public void Validate_EmptyQuestionId_ReturnsQuestionIdRequired()
    {
        _validator.Validate(new GetMathStepGradeQuery(Guid.NewGuid(), Guid.Empty)).Errors.Select(x => x.ErrorCode).Should().Equal(ErrorCodes.QuestionIdRequired);
    }
}
