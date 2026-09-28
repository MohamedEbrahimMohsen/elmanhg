using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Lessons.ReorderLesson;

public sealed class ReorderLessonValidator : AbstractValidator<ReorderLessonCommand>
{
    public ReorderLessonValidator()
    {
        RuleFor(x => x.LessonId).ValidateRequired(ErrorCodes.LessonIdRequired);
        RuleFor(x => x.Position).ValidateMin(1, ErrorCodes.LessonPositionInvalid);
    }
}
