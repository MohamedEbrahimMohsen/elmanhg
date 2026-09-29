using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.TeacherInbox.RecordVoiceDraft;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.TeacherInbox.RecordVoiceDraft;

public sealed class RecordVoiceDraftValidatorTests
{
    private const int MaxDurationSeconds = 180;
    private readonly RecordVoiceDraftValidator _validator = new(Options.Create(new AskTeacherOptions { VoiceMaxSizeInMb = 1, VoiceMaxDurationSeconds = MaxDurationSeconds }));

    [Fact]
    public void Validate_ValidWebm_Passes()
    {
        var result = Validate(TeacherVoiceSignatures.FormFile(TeacherVoiceSignatures.Webm));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("voice.ogg", "audio/ogg")]
    [InlineData("voice.m4a", "audio/mp4")]
    [InlineData("voice.mp4", "audio/mp4")]
    public void Validate_ValidOggAndM4a_Passes(string fileName, string contentType)
    {
        var bytes = fileName.EndsWith(".ogg", StringComparison.Ordinal) ? TeacherVoiceSignatures.Ogg : TeacherVoiceSignatures.M4a;

        var result = Validate(TeacherVoiceSignatures.FormFile(bytes, fileName, contentType));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("voice.webm", "audio/webm")]
    [InlineData("voice.ogg", "audio/ogg")]
    [InlineData("voice.m4a", "audio/mp4")]
    [InlineData("voice.mp4", "audio/mp4")]
    public void Validate_SignatureMismatchPerExtension_FailsTypeInvalid(string fileName, string contentType)
    {
        var result = Validate(TeacherVoiceSignatures.FormFile(TeacherVoiceSignatures.Png, fileName, contentType));

        result.Errors.Select(x => x.ErrorCode).Should().Equal(ErrorCodes.TeacherVoiceAudioTypeInvalid);
    }

    [Theory]
    [InlineData("voice.m4a")]
    [InlineData("voice.mp4")]
    public void Validate_TruncatedMp4Header_FailsTypeInvalid(string fileName)
    {
        var result = Validate(TeacherVoiceSignatures.FormFile([0x00, 0x00, 0x00, 0x20, 0x66, 0x74], fileName, "audio/mp4"));

        result.Errors.Select(x => x.ErrorCode).Should().Equal(ErrorCodes.TeacherVoiceAudioTypeInvalid);
    }

    [Fact]
    public void Validate_ContentTypeWithCodecs_Passes()
    {
        var result = Validate(TeacherVoiceSignatures.FormFile(TeacherVoiceSignatures.Webm, contentType: "audio/webm;codecs=opus"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_MissingAudio_FailsAudioRequired()
    {
        var result = Validate(null);

        result.Errors.Select(x => x.ErrorCode).Should().Equal(ErrorCodes.TeacherVoiceAudioRequired);
    }

    [Fact]
    public void Validate_EmptyAudio_FailsAudioRequired()
    {
        var result = Validate(TeacherVoiceSignatures.FormFile([]));

        result.Errors.Select(x => x.ErrorCode).Should().Equal(ErrorCodes.TeacherVoiceAudioRequired);
    }

    [Fact]
    public void Validate_DisallowedExtension_FailsTypeInvalid()
    {
        var result = Validate(TeacherVoiceSignatures.FormFile(TeacherVoiceSignatures.Webm, "voice.mp3"));

        result.Errors.Select(x => x.ErrorCode).Should().Contain(ErrorCodes.TeacherVoiceAudioTypeInvalid).And.OnlyContain(x => x == ErrorCodes.TeacherVoiceAudioTypeInvalid);
    }

    [Fact]
    public void Validate_WrongContentType_FailsTypeInvalid()
    {
        var result = Validate(TeacherVoiceSignatures.FormFile(TeacherVoiceSignatures.Webm, contentType: "image/png"));

        result.Errors.Select(x => x.ErrorCode).Should().Equal(ErrorCodes.TeacherVoiceAudioTypeInvalid);
    }

    [Fact]
    public void Validate_SignatureMismatch_FailsTypeInvalid()
    {
        var result = Validate(TeacherVoiceSignatures.FormFile(TeacherVoiceSignatures.Png));

        result.Errors.Select(x => x.ErrorCode).Should().Equal(ErrorCodes.TeacherVoiceAudioTypeInvalid);
    }

    [Fact]
    public void Validate_TooLarge_FailsTooLarge()
    {
        byte[] bytes = [.. TeacherVoiceSignatures.Webm, .. new byte[(1024 * 1024) + 1 - TeacherVoiceSignatures.Webm.Length]];

        var result = Validate(TeacherVoiceSignatures.FormFile(bytes));

        result.Errors.Select(x => x.ErrorCode).Should().Equal(ErrorCodes.TeacherVoiceAudioTooLarge);
    }

    [Fact]
    public void Validate_DurationZero_FailsDurationInvalid()
    {
        var result = Validate(TeacherVoiceSignatures.FormFile(TeacherVoiceSignatures.Webm), 0);

        result.Errors.Select(x => x.ErrorCode).Should().Equal(ErrorCodes.TeacherVoiceDurationInvalid);
    }

    [Fact]
    public void Validate_DurationAboveMax_FailsDurationInvalid()
    {
        var result = Validate(TeacherVoiceSignatures.FormFile(TeacherVoiceSignatures.Webm), MaxDurationSeconds + 1);

        result.Errors.Select(x => x.ErrorCode).Should().Equal(ErrorCodes.TeacherVoiceDurationInvalid);
    }

    private FluentValidation.Results.ValidationResult Validate(IFormFile? audio, int durationSeconds = 12) => _validator.Validate(new RecordVoiceDraftCommand(Guid.NewGuid(), audio, durationSeconds));
}
