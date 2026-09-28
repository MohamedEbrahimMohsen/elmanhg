using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Subjects.ReorderSubject;

public sealed class ReorderSubjectValidator : AbstractValidator<ReorderSubjectCommand>
{
    public ReorderSubjectValidator()
    {
        RuleFor(x => x.SubjectId).ValidateRequired(ErrorCodes.SubjectIdRequired);
        RuleFor(x => x.Position).ValidateMin(1, ErrorCodes.SubjectPositionInvalid);
    }
}
