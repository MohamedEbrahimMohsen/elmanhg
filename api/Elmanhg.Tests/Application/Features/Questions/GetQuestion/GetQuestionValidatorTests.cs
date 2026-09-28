using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.GetQuestion;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Questions.GetQuestion;

public sealed class GetQuestionValidatorTests
{
    private readonly GetQuestionValidator _validator = new();

    [Fact]
    public void Validate_ValidQuery_HasNoErrors()
    {
        _validator.Validate(new GetQuestionQuery(Guid.NewGuid())).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyQuestionId_HasQuestionIdRequired()
    {
        _validator.Validate(new GetQuestionQuery(Guid.Empty)).Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.QuestionIdRequired);
    }
}
