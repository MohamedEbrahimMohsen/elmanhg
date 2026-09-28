using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Lessons.GetLessons;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Lessons.GetLessons;

public sealed class GetLessonsValidatorTests
{
    private readonly GetLessonsValidator _validator = new();

    [Fact]
    public void Validate_ValidQuery_Passes()
    {
        var result = _validator.Validate(new GetLessonsQuery(Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyUnitId_FailsWithUnitIdRequired()
    {
        var result = _validator.Validate(new GetLessonsQuery(Guid.Empty));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.UnitIdRequired);
    }
}
