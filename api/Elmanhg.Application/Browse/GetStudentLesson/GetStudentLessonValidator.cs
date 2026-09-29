using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Browse.GetStudentLesson;

public sealed class GetStudentLessonValidator : AbstractValidator<GetStudentLessonQuery>
{
    public GetStudentLessonValidator()
    {
        RuleFor(x => x.LessonId).ValidateRequired(ErrorCodes.LessonIdRequired);
    }
}
