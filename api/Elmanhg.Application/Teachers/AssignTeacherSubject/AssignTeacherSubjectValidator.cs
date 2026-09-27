using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Teachers.AssignTeacherSubject;

public sealed class AssignTeacherSubjectValidator : AbstractValidator<AssignTeacherSubjectCommand>
{
    public AssignTeacherSubjectValidator()
    {
        RuleFor(x => x.TeacherId).ValidateRequired(ErrorCodes.TeacherIdRequired);
        RuleFor(x => x.SubjectId).ValidateRequired(ErrorCodes.SubjectIdRequired);
    }
}
