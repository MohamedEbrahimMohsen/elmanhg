using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Questions.Shared.Import;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Questions.PreviewQuestionImport;

public sealed class PreviewQuestionImportValidator : AbstractValidator<PreviewQuestionImportQuery>
{
    public PreviewQuestionImportValidator(IOptions<ContentOptions> contentOptions)
    {
        RuleFor(x => x.LessonId).ValidateRequired(ErrorCodes.LessonIdRequired);
        RuleFor(x => x.File).ValidateQuestionImportFile(contentOptions.Value);
    }
}
