using Elmanhg.Application.ExamBlueprints.DeleteExamBlueprint;
using Elmanhg.Application.Exceptions;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.ExamBlueprints.DeleteExamBlueprint;

public sealed class DeleteExamBlueprintValidatorTests
{
    private readonly DeleteExamBlueprintValidator _validator = new();

    [Fact]
    public void Validate_EmptyId_FailsExamBlueprintIdRequired()
    {
        var result = _validator.Validate(new DeleteExamBlueprintCommand(Guid.Empty));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.ExamBlueprintIdRequired);
    }

    [Fact]
    public void Validate_Id_Passes()
    {
        var result = _validator.Validate(new DeleteExamBlueprintCommand(Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }
}
