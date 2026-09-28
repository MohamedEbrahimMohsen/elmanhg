using Elmanhg.Application.ExamBlueprints.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Questions;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.ExamBlueprints.Shared;

public sealed class ExamBlueprintInputValidatorTests
{
    private readonly ExamBlueprintInputValidator _validator = new(Options.Create(new ExamBlueprintsOptions { MaxQuestionCount = 100, MaxTimeLimitMinutes = 300 }));

    [Fact]
    public void Validate_ValidInput_Passes()
    {
        var result = _validator.Validate(Input(mix: new ExamDifficultyMixInput(30, 50, 20)));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_TypeMissing_FailsQuestionTypeRequired()
    {
        var result = _validator.Validate(Input([new ExamTypeCountInput(null, 2)]));

        Codes(result).Should().Contain(ErrorCodes.QuestionTypeRequired);
    }

    [Fact]
    public void Validate_TypeUndefined_FailsQuestionTypeInvalid()
    {
        var result = _validator.Validate(Input([new ExamTypeCountInput((QuestionType)99, 2)]));

        Codes(result).Should().Contain(ErrorCodes.QuestionTypeInvalid);
    }

    [Fact]
    public void Validate_NegativeCount_FailsCountInvalid()
    {
        var result = _validator.Validate(Input([new ExamTypeCountInput(QuestionType.Mcq, 3), new ExamTypeCountInput(QuestionType.Fill, -1)]));

        Codes(result).Should().Contain(ErrorCodes.ExamBlueprintCountInvalid);
    }

    [Fact]
    public void Validate_CountAboveMax_FailsCountInvalid()
    {
        var result = _validator.Validate(Input([new ExamTypeCountInput(QuestionType.Mcq, 101)]));

        Codes(result).Should().Contain(ErrorCodes.ExamBlueprintCountInvalid);
    }

    [Fact]
    public void Validate_DuplicateType_FailsTypeDuplicate()
    {
        var result = _validator.Validate(Input([new ExamTypeCountInput(QuestionType.Mcq, 1), new ExamTypeCountInput(QuestionType.Mcq, 2)]));

        Codes(result).Should().Contain(ErrorCodes.ExamBlueprintTypeDuplicate);
    }

    [Fact]
    public void Validate_AllZero_FailsEmpty()
    {
        var result = _validator.Validate(Input([new ExamTypeCountInput(QuestionType.Mcq, 0), new ExamTypeCountInput(QuestionType.Fill, 0)]));

        Codes(result).Should().Contain(ErrorCodes.ExamBlueprintEmpty);
    }

    [Fact]
    public void Validate_NullTypeCounts_FailsEmpty()
    {
        var result = _validator.Validate(new ExamBlueprintInput(null!, null, null, 50));

        Codes(result).Should().Contain(ErrorCodes.ExamBlueprintEmpty);
    }

    [Fact]
    public void Validate_NullEntry_FailsTypeCountRequired()
    {
        var result = _validator.Validate(Input([new ExamTypeCountInput(QuestionType.Mcq, 1), null!]));

        Codes(result).Should().ContainSingle().Which.Should().Be(ErrorCodes.ExamBlueprintTypeCountRequired);
    }

    [Fact]
    public void Validate_TotalAboveMax_FailsTooLarge()
    {
        var result = _validator.Validate(Input([new ExamTypeCountInput(QuestionType.Mcq, 60), new ExamTypeCountInput(QuestionType.Fill, 50)]));

        Codes(result).Should().Contain(ErrorCodes.ExamBlueprintTooLarge);
    }

    [Fact]
    public void Validate_MixPercentOutOfRange_FailsDifficultyMixInvalid()
    {
        var result = _validator.Validate(Input(mix: new ExamDifficultyMixInput(120, -10, -10)));

        Codes(result).Should().Contain(ErrorCodes.ExamBlueprintDifficultyMixInvalid);
    }

    [Fact]
    public void Validate_MixNotSummingTo100_FailsDifficultyMixInvalid()
    {
        var result = _validator.Validate(Input(mix: new ExamDifficultyMixInput(30, 30, 30)));

        Codes(result).Should().Contain(ErrorCodes.ExamBlueprintDifficultyMixInvalid);
    }

    [Fact]
    public void Validate_TimeLimitZero_FailsTimeLimitInvalid()
    {
        var result = _validator.Validate(Input(timeLimitMinutes: 0));

        Codes(result).Should().Contain(ErrorCodes.ExamBlueprintTimeLimitInvalid);
    }

    [Fact]
    public void Validate_TimeLimitAboveMax_FailsTimeLimitInvalid()
    {
        var result = _validator.Validate(Input(timeLimitMinutes: 301));

        Codes(result).Should().Contain(ErrorCodes.ExamBlueprintTimeLimitInvalid);
    }

    [Fact]
    public void Validate_NoTimeLimitNoMix_Passes()
    {
        var result = _validator.Validate(new ExamBlueprintInput([new ExamTypeCountInput(QuestionType.Mcq, 1)], null, null, 50));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Validate_PassMarkOutOfRange_FailsPassMarkInvalid(int passMark)
    {
        var result = _validator.Validate(Input(passMark: passMark));

        Codes(result).Should().Contain(ErrorCodes.ExamBlueprintPassMarkInvalid);
    }

    private static ExamBlueprintInput Input(List<ExamTypeCountInput>? typeCounts = null, ExamDifficultyMixInput? mix = null, int? timeLimitMinutes = 45, int passMark = 50)
    {
        return new ExamBlueprintInput(typeCounts ?? [new ExamTypeCountInput(QuestionType.Mcq, 10), new ExamTypeCountInput(QuestionType.Fill, 5)], mix, timeLimitMinutes, passMark);
    }

    private static IEnumerable<string> Codes(FluentValidation.Results.ValidationResult result) => result.Errors.Select(x => x.ErrorCode);
}
