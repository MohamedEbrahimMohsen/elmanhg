using Elmanhg.Application.Exceptions;
using Elmanhg.Application.QuestionValidation.BulkApproveQuestions;
using Elmanhg.Application.Shared.Options;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.QuestionValidation.BulkApproveQuestions;

public sealed class BulkApproveQuestionsValidatorTests
{
    private readonly BulkApproveQuestionsValidator _validator = new(Options.Create(new QuestionValidationOptions { BulkApproveMaxCount = 3 }));

    [Fact]
    public void Validate_Valid_Passes()
    {
        _validator.Validate(new BulkApproveQuestionsCommand(Guid.NewGuid(), [Guid.NewGuid(), Guid.NewGuid()])).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptySession_FailsReviewSessionIdRequired()
    {
        Codes(new BulkApproveQuestionsCommand(Guid.Empty, [Guid.NewGuid()])).Should().Contain(ErrorCodes.ReviewSessionIdRequired);
    }

    [Fact]
    public void Validate_EmptyList_FailsQuestionIdsRequired()
    {
        Codes(new BulkApproveQuestionsCommand(Guid.NewGuid(), [])).Should().Contain(ErrorCodes.QuestionIdsRequired);
    }

    [Fact]
    public void Validate_OverMax_FailsQuestionIdsTooMany()
    {
        Codes(new BulkApproveQuestionsCommand(Guid.NewGuid(), [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()])).Should().Contain(ErrorCodes.QuestionIdsTooMany);
    }

    [Fact]
    public void Validate_Duplicates_FailsQuestionIdsDuplicate()
    {
        var questionId = Guid.NewGuid();

        Codes(new BulkApproveQuestionsCommand(Guid.NewGuid(), [questionId, questionId])).Should().Contain(ErrorCodes.QuestionIdsDuplicate);
    }

    [Fact]
    public void Validate_EmptyGuidItem_FailsQuestionIdRequired()
    {
        Codes(new BulkApproveQuestionsCommand(Guid.NewGuid(), [Guid.Empty])).Should().Contain(ErrorCodes.QuestionIdRequired);
    }

    private List<string> Codes(BulkApproveQuestionsCommand command)
    {
        return _validator.Validate(command).Errors
            .Select(x => x.ErrorCode)
            .ToList();
    }
}
