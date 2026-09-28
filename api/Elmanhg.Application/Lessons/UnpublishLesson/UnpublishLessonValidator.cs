using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Lessons.UnpublishLesson;

public sealed class UnpublishLessonValidator : AbstractValidator<UnpublishLessonCommand>
{
    public UnpublishLessonValidator()
    {
        RuleFor(x => x.LessonId).ValidateRequired(ErrorCodes.LessonIdRequired);
    }
}
