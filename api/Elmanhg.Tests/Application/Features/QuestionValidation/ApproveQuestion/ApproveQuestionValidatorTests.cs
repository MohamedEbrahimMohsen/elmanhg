using Elmanhg.Application.Exceptions;
using Elmanhg.Application.QuestionValidation.ApproveQuestion;
using Elmanhg.Domain.Questions;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.QuestionValidation.ApproveQuestion;

public sealed class ApproveQuestionValidatorTests
{
    private readonly ApproveQuestionValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        _validator.Validate(Command()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyQuestionId_FailsQuestionIdRequired()
    {
        Codes(Command() with { QuestionId = Guid.Empty }).Should().Contain(ErrorCodes.QuestionIdRequired);
    }

    [Fact]
    public void Validate_VersionZero_FailsQuestionVersionInvalid()
    {
        Codes(Command() with { Version = 0 }).Should().Contain(ErrorCodes.QuestionVersionInvalid);
    }

    [Fact]
    public void Validate_UnknownDifficulty_FailsQuestionDifficultyInvalid()
    {
        Codes(Command() with { Difficulty = (QuestionDifficulty)99 }).Should().Contain(ErrorCodes.QuestionDifficultyInvalid);
    }

    private static ApproveQuestionCommand Command() => new(Guid.NewGuid(), 1, QuestionDifficulty.Hard);

    private List<string> Codes(ApproveQuestionCommand command)
    {
        return _validator.Validate(command).Errors
            .Select(x => x.ErrorCode)
            .ToList();
    }
}
