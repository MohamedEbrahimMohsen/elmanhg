using Elmanhg.Application.Browse.GetStudentUnit;
using Elmanhg.Application.Exceptions;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Browse.GetStudentUnit;

public sealed class GetStudentUnitValidatorTests
{
    private readonly GetStudentUnitValidator _validator = new();

    [Fact]
    public void Validate_UnitId_Passes()
    {
        var result = _validator.Validate(new GetStudentUnitQuery(Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyUnitId_FailsWithUnitIdRequired()
    {
        var result = _validator.Validate(new GetStudentUnitQuery(Guid.Empty));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.UnitIdRequired);
    }
}
