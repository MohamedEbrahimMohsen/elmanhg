using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.RetireQuestion;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Questions.RetireQuestion;

public sealed class RetireQuestionValidatorTests
{
    private readonly RetireQuestionValidator _validator = new();

    [Fact]
    public void Validate_QuestionId_Passes()
    {
        var result = _validator.Validate(new RetireQuestionCommand(Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyQuestionId_FailsWithQuestionIdRequired()
    {
        var result = _validator.Validate(new RetireQuestionCommand(Guid.Empty));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.QuestionIdRequired);
    }
}
