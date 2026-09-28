using Elmanhg.Application.Exams.GetMultiUnitExamOverview;
using Elmanhg.Application.Exceptions;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Exams.GetMultiUnitExamOverview;

public sealed class GetMultiUnitExamOverviewValidatorTests
{
    private readonly GetMultiUnitExamOverviewValidator _validator = new();

    [Fact]
    public void Validate_EmptySubjectId_ReturnsSubjectIdRequired()
    {
        _validator.Validate(new GetMultiUnitExamOverviewQuery(Guid.Empty)).Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.SubjectIdRequired);
    }

    [Fact]
    public void Validate_SubjectId_Passes()
    {
        _validator.Validate(new GetMultiUnitExamOverviewQuery(Guid.NewGuid())).IsValid.Should().BeTrue();
    }
}
