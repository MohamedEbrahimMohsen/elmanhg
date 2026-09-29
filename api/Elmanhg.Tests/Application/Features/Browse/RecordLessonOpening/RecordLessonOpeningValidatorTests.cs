using Elmanhg.Application.Browse.RecordLessonOpening;
using Elmanhg.Application.Exceptions;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Browse.RecordLessonOpening;

public sealed class RecordLessonOpeningValidatorTests
{
    private readonly RecordLessonOpeningValidator _validator = new();

    [Fact]
    public void Validate_LessonId_Passes()
    {
        var result = _validator.Validate(new RecordLessonOpeningCommand(Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyLessonId_FailsWithLessonIdRequired()
    {
        var result = _validator.Validate(new RecordLessonOpeningCommand(Guid.Empty));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.LessonIdRequired);
    }
}
