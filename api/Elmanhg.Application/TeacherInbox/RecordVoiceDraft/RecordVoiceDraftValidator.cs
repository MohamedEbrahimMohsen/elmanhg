using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.RuntimeSettings;
using Elmanhg.Application.Shared.RuntimeSettings.Definitions;
using FluentValidation;

namespace Elmanhg.Application.TeacherInbox.RecordVoiceDraft;

public sealed class RecordVoiceDraftValidator : AbstractValidator<RecordVoiceDraftCommand>
{
    public RecordVoiceDraftValidator(IRuntimeSettings runtimeSettings)
    {
        RuleFor(x => x.Audio)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .WithErrorCode(ErrorCodes.TeacherVoiceAudioRequired)
            .ValidateRequired(ErrorCodes.TeacherVoiceAudioRequired)
            .ValidateAllowedExtensions(TeacherVoiceFormats.Extensions, ErrorCodes.TeacherVoiceAudioTypeInvalid)
            .MustAsync(async (file, cancellationToken) => file is null || file.Length <= await runtimeSettings.GetAsync(UploadRuntimeSettings.VoiceReplyMaxSizeInMb, cancellationToken).ConfigureAwait(false) * UploadRuntimeSettings.BytesPerMegabyte)
            .WithErrorCode(ErrorCodes.TeacherVoiceAudioTooLarge);
        RuleFor(x => x.Audio)
            .Must(file => file is null || TeacherVoiceFormats.HasAllowedMediaType(file))
            .WithErrorCode(ErrorCodes.TeacherVoiceAudioTypeInvalid);
        RuleFor(x => x.Audio).ValidateFileSignature(TeacherVoiceFormats.Signatures, ErrorCodes.TeacherVoiceAudioTypeInvalid).When(x => x.Audio is { Length: > 0 });
        RuleFor(x => x.DurationSeconds)
            .MustAsync(async (seconds, cancellationToken) => seconds >= 1 && seconds <= await runtimeSettings.GetAsync(UploadRuntimeSettings.VoiceReplyMaxDurationSeconds, cancellationToken).ConfigureAwait(false))
            .WithErrorCode(ErrorCodes.TeacherVoiceDurationInvalid);
    }
}
