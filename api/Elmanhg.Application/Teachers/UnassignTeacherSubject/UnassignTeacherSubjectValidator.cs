using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Teachers.UnassignTeacherSubject;

public sealed class UnassignTeacherSubjectValidator : AbstractValidator<UnassignTeacherSubjectCommand>
{
    public UnassignTeacherSubjectValidator()
    {
        RuleFor(x => x.TeacherId).ValidateRequired(ErrorCodes.TeacherIdRequired);
        RuleFor(x => x.SubjectId).ValidateRequired(ErrorCodes.SubjectIdRequired);
    }
}
