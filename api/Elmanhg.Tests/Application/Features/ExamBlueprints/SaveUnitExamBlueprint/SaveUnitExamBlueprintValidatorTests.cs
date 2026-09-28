using Elmanhg.Application.ExamBlueprints.SaveUnitExamBlueprint;
using Elmanhg.Application.ExamBlueprints.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Questions;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.ExamBlueprints.SaveUnitExamBlueprint;

public sealed class SaveUnitExamBlueprintValidatorTests
{
    private static readonly ExamBlueprintInput Valid = new([new ExamTypeCountInput(QuestionType.Mcq, 2)], null, 45, 50);
    private readonly SaveUnitExamBlueprintValidator _validator = new(Options.Create(new ExamBlueprintsOptions()));

    [Fact]
    public void Validate_EmptyUnitId_FailsUnitIdRequired()
    {
        var result = _validator.Validate(new SaveUnitExamBlueprintCommand(Guid.Empty, Valid));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.UnitIdRequired);
    }

    [Fact]
    public void Validate_InvalidBlueprint_FailsWithNestedCode()
    {
        var result = _validator.Validate(new SaveUnitExamBlueprintCommand(Guid.NewGuid(), Valid with { PassMark = 0 }));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.ExamBlueprintPassMarkInvalid);
    }

    [Fact]
    public void Validate_Valid_Passes()
    {
        var result = _validator.Validate(new SaveUnitExamBlueprintCommand(Guid.NewGuid(), Valid));

        result.IsValid.Should().BeTrue();
    }
}
