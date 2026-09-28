using Elmanhg.Application.ExamBlueprints.GetSubjectExamBlueprints;
using Elmanhg.Application.Exceptions;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.ExamBlueprints.GetSubjectExamBlueprints;

public sealed class GetSubjectExamBlueprintsValidatorTests
{
    private readonly GetSubjectExamBlueprintsValidator _validator = new();

    [Fact]
    public void Validate_EmptySubjectId_FailsSubjectIdRequired()
    {
        var result = _validator.Validate(new GetSubjectExamBlueprintsQuery(Guid.Empty));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.SubjectIdRequired);
    }

    [Fact]
    public void Validate_SubjectId_Passes()
    {
        var result = _validator.Validate(new GetSubjectExamBlueprintsQuery(Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }
}
