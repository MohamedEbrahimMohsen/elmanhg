using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.TeacherInbox.RecordVoiceDraft;

public sealed class RecordVoiceDraftValidator : AbstractValidator<RecordVoiceDraftCommand>
{
    public RecordVoiceDraftValidator(IOptions<AskTeacherOptions> askTeacherOptions)
    {
        var options = askTeacherOptions.Value;

        RuleFor(x => x.Audio)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .WithErrorCode(ErrorCodes.TeacherVoiceAudioRequired)
            .ValidateRequired(ErrorCodes.TeacherVoiceAudioRequired)
            .ValidateAllowedExtensions(TeacherVoiceFormats.Extensions, ErrorCodes.TeacherVoiceAudioTypeInvalid)
            .ValidateMaxFileSize(options.VoiceMaxSizeInMb, ErrorCodes.TeacherVoiceAudioTooLarge);
        RuleFor(x => x.Audio)
            .Must(file => file is null || TeacherVoiceFormats.HasAllowedMediaType(file))
            .WithErrorCode(ErrorCodes.TeacherVoiceAudioTypeInvalid);
        RuleFor(x => x.Audio)
            .Must(file => file is null || file.Length == 0 || TeacherVoiceFormats.HasMatchingSignature(file))
            .WithErrorCode(ErrorCodes.TeacherVoiceAudioTypeInvalid);
        RuleFor(x => x.DurationSeconds).ValidateRange(1, options.VoiceMaxDurationSeconds, ErrorCodes.TeacherVoiceDurationInvalid);
    }
}
