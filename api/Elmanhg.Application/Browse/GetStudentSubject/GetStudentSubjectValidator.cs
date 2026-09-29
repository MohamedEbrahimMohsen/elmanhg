using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Browse.GetStudentSubject;

public sealed class GetStudentSubjectValidator : AbstractValidator<GetStudentSubjectQuery>
{
    public GetStudentSubjectValidator()
    {
        RuleFor(x => x.SubjectId).ValidateRequired(ErrorCodes.SubjectIdRequired);
    }
}
