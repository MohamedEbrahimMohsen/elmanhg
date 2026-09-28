using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Lessons.GetLesson;

public sealed class GetLessonValidator : AbstractValidator<GetLessonQuery>
{
    public GetLessonValidator()
    {
        RuleFor(x => x.LessonId).ValidateRequired(ErrorCodes.LessonIdRequired);
    }
}
