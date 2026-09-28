using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Lessons.UploadLessonImage;

public sealed class UploadLessonImageValidator : AbstractValidator<UploadLessonImageCommand>
{
    public UploadLessonImageValidator(IOptions<ContentOptions> contentOptions)
    {
        var options = contentOptions.Value;

        RuleFor(x => x.LessonId).ValidateRequired(ErrorCodes.LessonIdRequired);
        RuleFor(x => x.File)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .WithErrorCode(ErrorCodes.LessonImageRequired)
            .ValidateRequired(ErrorCodes.LessonImageRequired)
            .ValidateAllowedExtensions(LessonImageFormats.Extensions, ErrorCodes.LessonImageTypeInvalid)
            .ValidateMaxFileSize(options.LessonImageMaxSizeInMb, ErrorCodes.LessonImageTooLarge);
        RuleFor(x => x.File)
            .Must(file => file is null || LessonImageFormats.ContentTypes.Contains(file.ContentType))
            .WithErrorCode(ErrorCodes.LessonImageTypeInvalid);
    }
}
