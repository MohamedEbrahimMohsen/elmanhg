using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Lessons.UploadDiagramImage;

public sealed class UploadDiagramImageValidator : AbstractValidator<UploadDiagramImageCommand>
{
    public UploadDiagramImageValidator(IOptions<ContentOptions> contentOptions)
    {
        var options = contentOptions.Value;

        RuleFor(x => x.LessonId).ValidateRequired(ErrorCodes.LessonIdRequired);
        RuleFor(x => x.File)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .WithErrorCode(ErrorCodes.LessonImageRequired)
            .ValidateRequired(ErrorCodes.LessonImageRequired)
            .ValidateAllowedExtensions(DiagramImageFormats.Extensions, ErrorCodes.QuestionDiagramImageTypeInvalid)
            .ValidateMaxFileSize(options.LessonImageMaxSizeInMb, ErrorCodes.LessonImageTooLarge);
        RuleFor(x => x.File)
            .Must(file => file is null || DiagramImageFormats.ContentTypes.Contains(file.ContentType))
            .WithErrorCode(ErrorCodes.QuestionDiagramImageTypeInvalid);
        RuleFor(x => x.File).ValidateFileSignature(DiagramImageFormats.Signatures, ErrorCodes.QuestionDiagramImageTypeInvalid).When(x => x.File is { Length: > 0 });
    }
}
