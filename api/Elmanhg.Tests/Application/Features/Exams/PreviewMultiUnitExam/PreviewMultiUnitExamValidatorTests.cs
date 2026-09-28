using Elmanhg.Application.Exams.PreviewMultiUnitExam;
using Elmanhg.Application.Exams.Shared;
using Elmanhg.Application.Exceptions;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Exams.PreviewMultiUnitExam;

public sealed class PreviewMultiUnitExamValidatorTests
{
    private readonly PreviewMultiUnitExamValidator _validator = new();

    [Fact]
    public void Validate_NullSelection_ReturnsUnitsTooFew()
    {
        _validator.Validate(new PreviewMultiUnitExamQuery(null!)).Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.MultiUnitExamUnitsTooFew);
    }

    [Fact]
    public void Validate_OneUnit_ReturnsUnitsTooFew()
    {
        var query = new PreviewMultiUnitExamQuery(new MultiUnitExamSelection(Guid.NewGuid(), [Guid.NewGuid()], 20));

        _validator.Validate(query).Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.MultiUnitExamUnitsTooFew);
    }
}
