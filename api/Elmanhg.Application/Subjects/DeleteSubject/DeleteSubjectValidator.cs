using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Subjects.DeleteSubject;

public sealed class DeleteSubjectValidator : AbstractValidator<DeleteSubjectCommand>
{
    public DeleteSubjectValidator()
    {
        RuleFor(x => x.SubjectId).ValidateRequired(ErrorCodes.SubjectIdRequired);
    }
}
