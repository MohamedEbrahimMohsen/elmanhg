using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.ContentRetrieval.ReindexLessonContent;

public sealed class ReindexLessonContentValidator : AbstractValidator<ReindexLessonContentCommand>
{
    public ReindexLessonContentValidator()
    {
        RuleFor(x => x.LessonId).ValidateRequired(ErrorCodes.LessonIdRequired);
    }
}
