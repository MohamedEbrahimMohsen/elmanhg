using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Application.Shared.RuntimeSettings;
using Elmanhg.Application.Shared.RuntimeSettings.Definitions;
using Elmanhg.Application.TeacherThreads.Shared;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.TeacherThreads.CreateTeacherThread;

public sealed class CreateTeacherThreadValidator : AbstractValidator<CreateTeacherThreadCommand>
{
    public CreateTeacherThreadValidator(IOptions<AskTeacherOptions> askTeacherOptions, IRuntimeSettings runtimeSettings)
    {
        var options = askTeacherOptions.Value;

        RuleFor(x => x.Text)
            .Cascade(CascadeMode.Stop)
            .ValidateRequired(ErrorCodes.TeacherThreadTextRequired)
            .ValidateMaxLength(options.QuestionTextMaxLength, ErrorCodes.TeacherThreadTextTooLong);
        RuleFor(x => x)
            .Must(x => TeacherThreadContextRules.HasExactlyOne(x.LessonId, x.QuestionId, x.AttemptId))
            .WithErrorCode(ErrorCodes.TeacherThreadContextInvalid);
        RuleFor(x => x.Image)
            .ValidateAllowedExtensions(TeacherThreadImageFormats.Extensions, ErrorCodes.TeacherThreadImageTypeInvalid);
        RuleFor(x => x.Image)
            .MustAsync(async (file, cancellationToken) => file is null || file.Length <= await runtimeSettings.GetAsync(UploadRuntimeSettings.AskTeacherImageMaxSizeInMb, cancellationToken).ConfigureAwait(false) * UploadRuntimeSettings.BytesPerMegabyte)
            .WithErrorCode(ErrorCodes.TeacherThreadImageTooLarge);
        RuleFor(x => x.Image)
            .Must(file => file is null || TeacherThreadImageFormats.ContentTypes.Contains(file.ContentType))
            .WithErrorCode(ErrorCodes.TeacherThreadImageTypeInvalid);
        RuleFor(x => x.Image)
            .Must(file => file is null || TeacherThreadImageFormats.HasMatchingSignature(file))
            .WithErrorCode(ErrorCodes.TeacherThreadImageTypeInvalid);
    }
}
