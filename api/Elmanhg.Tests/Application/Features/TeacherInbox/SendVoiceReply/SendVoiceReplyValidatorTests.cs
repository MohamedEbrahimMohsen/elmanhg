using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.TeacherInbox.SendVoiceReply;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.TeacherInbox.SendVoiceReply;

public sealed class SendVoiceReplyValidatorTests
{
    private const int MaxLength = 10;
    private readonly SendVoiceReplyValidator _validator = new(Options.Create(new AskTeacherOptions { ReplyTextMaxLength = MaxLength }));

    [Fact]
    public void Validate_Valid_Passes()
    {
        var result = _validator.Validate(new SendVoiceReplyCommand(Guid.NewGuid(), Guid.NewGuid(), "Because."));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyDraftId_FailsDraftIdRequired()
    {
        var result = _validator.Validate(new SendVoiceReplyCommand(Guid.NewGuid(), Guid.Empty, "Because."));

        result.Errors.Select(x => x.ErrorCode).Should().Equal(ErrorCodes.TeacherVoiceDraftIdRequired);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Validate_BlankText_FailsReplyTextRequired(string? text)
    {
        var result = _validator.Validate(new SendVoiceReplyCommand(Guid.NewGuid(), Guid.NewGuid(), text));

        result.Errors.Select(x => x.ErrorCode).Should().Equal(ErrorCodes.TeacherThreadReplyTextRequired);
    }

    [Fact]
    public void Validate_TooLongText_FailsReplyTextTooLong()
    {
        var result = _validator.Validate(new SendVoiceReplyCommand(Guid.NewGuid(), Guid.NewGuid(), new string('a', MaxLength + 1)));

        result.Errors.Select(x => x.ErrorCode).Should().Equal(ErrorCodes.TeacherThreadReplyTextTooLong);
    }
}
