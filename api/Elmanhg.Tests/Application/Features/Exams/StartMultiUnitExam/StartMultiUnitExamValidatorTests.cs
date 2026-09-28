using Elmanhg.Application.Exams.Shared;
using Elmanhg.Application.Exams.StartMultiUnitExam;
using Elmanhg.Application.Exceptions;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Exams.StartMultiUnitExam;

public sealed class StartMultiUnitExamValidatorTests
{
    private readonly StartMultiUnitExamValidator _validator = new();

    [Fact]
    public void Validate_NullSelection_ReturnsUnitsTooFew()
    {
        _validator.Validate(new StartMultiUnitExamCommand(null!)).Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.MultiUnitExamUnitsTooFew);
    }

    [Fact]
    public void Validate_SizeInvalid_ReturnsSizeInvalid()
    {
        var command = new StartMultiUnitExamCommand(new MultiUnitExamSelection(Guid.NewGuid(), [Guid.NewGuid(), Guid.NewGuid()], 25));

        _validator.Validate(command).Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.MultiUnitExamSizeInvalid);
    }
}
