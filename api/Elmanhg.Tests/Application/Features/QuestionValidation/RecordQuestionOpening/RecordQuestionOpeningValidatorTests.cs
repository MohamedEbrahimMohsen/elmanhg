using Elmanhg.Application.Exceptions;
using Elmanhg.Application.QuestionValidation.RecordQuestionOpening;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.QuestionValidation.RecordQuestionOpening;

public sealed class RecordQuestionOpeningValidatorTests
{
    private readonly RecordQuestionOpeningValidator _validator = new();

    [Fact]
    public void Validate_Valid_Passes()
    {
        _validator.Validate(new RecordQuestionOpeningCommand(Guid.NewGuid(), Guid.NewGuid())).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptySession_FailsReviewSessionIdRequired()
    {
        Codes(new RecordQuestionOpeningCommand(Guid.Empty, Guid.NewGuid())).Should().Contain(ErrorCodes.ReviewSessionIdRequired);
    }

    [Fact]
    public void Validate_EmptyQuestion_FailsQuestionIdRequired()
    {
        Codes(new RecordQuestionOpeningCommand(Guid.NewGuid(), Guid.Empty)).Should().Contain(ErrorCodes.QuestionIdRequired);
    }

    private List<string> Codes(RecordQuestionOpeningCommand command)
    {
        return _validator.Validate(command).Errors
            .Select(x => x.ErrorCode)
            .ToList();
    }
}
