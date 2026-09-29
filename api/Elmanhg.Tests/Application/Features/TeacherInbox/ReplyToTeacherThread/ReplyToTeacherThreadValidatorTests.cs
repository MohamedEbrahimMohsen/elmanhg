using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.TeacherInbox.ReplyToTeacherThread;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.TeacherInbox.ReplyToTeacherThread;

public sealed class ReplyToTeacherThreadValidatorTests
{
    private const int MaxLength = 10;
    private readonly ReplyToTeacherThreadValidator _validator = new(Options.Create(new AskTeacherOptions { ReplyTextMaxLength = MaxLength }));

    [Fact]
    public void Validate_Text_Passes()
    {
        var result = _validator.Validate(new ReplyToTeacherThreadCommand(Guid.NewGuid(), "Because."));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_TextAtMaxLength_Passes()
    {
        var result = _validator.Validate(new ReplyToTeacherThreadCommand(Guid.NewGuid(), new string('a', MaxLength)));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Validate_BlankText_FailsWithReplyTextRequired(string? text)
    {
        var result = _validator.Validate(new ReplyToTeacherThreadCommand(Guid.NewGuid(), text));

        result.Errors.Select(x => x.ErrorCode).Should().Equal(ErrorCodes.TeacherThreadReplyTextRequired);
    }

    [Fact]
    public void Validate_TextOverMax_FailsWithReplyTextTooLong()
    {
        var result = _validator.Validate(new ReplyToTeacherThreadCommand(Guid.NewGuid(), new string('a', MaxLength + 1)));

        result.Errors.Select(x => x.ErrorCode).Should().Equal(ErrorCodes.TeacherThreadReplyTextTooLong);
    }
}
