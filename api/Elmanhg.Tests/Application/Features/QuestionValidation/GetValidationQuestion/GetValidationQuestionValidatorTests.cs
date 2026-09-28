using Elmanhg.Application.Exceptions;
using Elmanhg.Application.QuestionValidation.GetValidationQuestion;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.QuestionValidation.GetValidationQuestion;

public sealed class GetValidationQuestionValidatorTests
{
    private readonly GetValidationQuestionValidator _validator = new();

    [Fact]
    public void Validate_Valid_Passes()
    {
        _validator.Validate(new GetValidationQuestionQuery(Guid.NewGuid())).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyId_FailsQuestionIdRequired()
    {
        _validator.Validate(new GetValidationQuestionQuery(Guid.Empty)).Errors
            .Select(x => x.ErrorCode)
            .Should().Contain(ErrorCodes.QuestionIdRequired);
    }
}
