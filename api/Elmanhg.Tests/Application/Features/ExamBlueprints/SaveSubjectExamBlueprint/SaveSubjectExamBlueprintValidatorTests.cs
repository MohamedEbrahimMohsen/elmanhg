using Elmanhg.Application.ExamBlueprints.SaveSubjectExamBlueprint;
using Elmanhg.Application.ExamBlueprints.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Questions;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.ExamBlueprints.SaveSubjectExamBlueprint;

public sealed class SaveSubjectExamBlueprintValidatorTests
{
    private static readonly ExamBlueprintInput Valid = new([new ExamTypeCountInput(QuestionType.Mcq, 2)], null, 45, 50);
    private readonly SaveSubjectExamBlueprintValidator _validator = new(Options.Create(new ExamBlueprintsOptions()));

    [Fact]
    public void Validate_EmptySubjectId_FailsSubjectIdRequired()
    {
        var result = _validator.Validate(new SaveSubjectExamBlueprintCommand(Guid.Empty, Valid));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.SubjectIdRequired);
    }

    [Fact]
    public void Validate_InvalidBlueprint_FailsWithNestedCode()
    {
        var result = _validator.Validate(new SaveSubjectExamBlueprintCommand(Guid.NewGuid(), Valid with { TypeCounts = [new ExamTypeCountInput(QuestionType.Mcq, 0)] }));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.ExamBlueprintEmpty);
    }

    [Fact]
    public void Validate_NullBlueprint_FailsEmpty()
    {
        var result = _validator.Validate(new SaveSubjectExamBlueprintCommand(Guid.NewGuid(), null!));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.ExamBlueprintEmpty);
    }

    [Fact]
    public void Validate_Valid_Passes()
    {
        var result = _validator.Validate(new SaveSubjectExamBlueprintCommand(Guid.NewGuid(), Valid));

        result.IsValid.Should().BeTrue();
    }
}
