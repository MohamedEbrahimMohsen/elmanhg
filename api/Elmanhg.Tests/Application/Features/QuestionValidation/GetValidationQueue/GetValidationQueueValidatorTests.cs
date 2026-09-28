using Elmanhg.Application.Exceptions;
using Elmanhg.Application.QuestionValidation.GetValidationQueue;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Questions;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.QuestionValidation.GetValidationQueue;

public sealed class GetValidationQueueValidatorTests
{
    private readonly GetValidationQueueValidator _validator = new(Options.Create(new QuestionValidationOptions()));

    [Fact]
    public void Validate_Defaults_Passes()
    {
        _validator.Validate(Query()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_PageZero_FailsQuestionPageNumberInvalid()
    {
        Codes(Query() with { PageNumber = 0 }).Should().Contain(ErrorCodes.QuestionPageNumberInvalid);
    }

    [Fact]
    public void Validate_PageSizeOverMax_FailsQuestionPageSizeInvalid()
    {
        Codes(Query() with { PageSize = 101 }).Should().Contain(ErrorCodes.QuestionPageSizeInvalid);
    }

    [Fact]
    public void Validate_UnknownType_FailsQuestionTypeInvalid()
    {
        Codes(Query() with { Type = (QuestionType)99 }).Should().Contain(ErrorCodes.QuestionTypeInvalid);
    }

    [Fact]
    public void Validate_UnknownDifficulty_FailsQuestionDifficultyInvalid()
    {
        Codes(Query() with { Difficulty = (QuestionDifficulty)99 }).Should().Contain(ErrorCodes.QuestionDifficultyInvalid);
    }

    [Fact]
    public void Validate_AgeZero_FailsQuestionAgeFilterInvalid()
    {
        Codes(Query() with { MinAgeDays = 0 }).Should().Contain(ErrorCodes.QuestionAgeFilterInvalid);
    }

    [Fact]
    public void Validate_AgeOverMax_FailsQuestionAgeFilterInvalid()
    {
        Codes(Query() with { MinAgeDays = 366 }).Should().Contain(ErrorCodes.QuestionAgeFilterInvalid);
    }

    private static GetValidationQueueQuery Query() => new(null, null, null, null, null, null);

    private List<string> Codes(GetValidationQueueQuery query)
    {
        return _validator.Validate(query).Errors
            .Select(x => x.ErrorCode)
            .ToList();
    }
}
