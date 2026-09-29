using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.TeacherThreads.FollowUpTeacherThread;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.TeacherThreads.FollowUpTeacherThread;

public sealed class FollowUpTeacherThreadValidatorTests
{
    private const int MaxLength = 10;
    private readonly FollowUpTeacherThreadValidator _validator = new(Options.Create(new AskTeacherOptions { QuestionTextMaxLength = MaxLength }));

    [Fact]
    public void Validate_ValidText_Passes()
    {
        var result = _validator.Validate(new FollowUpTeacherThreadCommand(Guid.NewGuid(), new string('a', MaxLength)));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Validate_BlankText_FailsTextRequired(string? text)
    {
        var result = _validator.Validate(new FollowUpTeacherThreadCommand(Guid.NewGuid(), text));

        result.Errors.Select(x => x.ErrorCode).Should().Equal(ErrorCodes.TeacherThreadTextRequired);
    }

    [Fact]
    public void Validate_TooLong_FailsTextTooLong()
    {
        var result = _validator.Validate(new FollowUpTeacherThreadCommand(Guid.NewGuid(), new string('a', MaxLength + 1)));

        result.Errors.Select(x => x.ErrorCode).Should().Equal(ErrorCodes.TeacherThreadTextTooLong);
    }
}
