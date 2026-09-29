using Elmanhg.Application.ContentRetrieval.ReindexLessonContent;
using Elmanhg.Application.Exceptions;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.ContentRetrieval.ReindexLessonContent;

public sealed class ReindexLessonContentValidatorTests
{
    private readonly ReindexLessonContentValidator _validator = new();

    [Fact]
    public void Validate_EmptyLessonId_FailsLessonIdRequired()
    {
        var result = _validator.Validate(new ReindexLessonContentCommand(Guid.Empty));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.LessonIdRequired);
    }

    [Fact]
    public void Validate_LessonId_Passes()
    {
        var result = _validator.Validate(new ReindexLessonContentCommand(Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }
}
