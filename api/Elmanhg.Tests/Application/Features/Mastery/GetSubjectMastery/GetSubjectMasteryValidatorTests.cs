using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Mastery.GetSubjectMastery;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Mastery.GetSubjectMastery;

public sealed class GetSubjectMasteryValidatorTests
{
    private readonly GetSubjectMasteryValidator _validator = new();

    [Fact]
    public void Validate_SubjectId_Passes()
    {
        var result = _validator.Validate(new GetSubjectMasteryQuery(Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptySubjectId_FailsWithSubjectIdRequired()
    {
        var result = _validator.Validate(new GetSubjectMasteryQuery(Guid.Empty));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.SubjectIdRequired);
    }
}
