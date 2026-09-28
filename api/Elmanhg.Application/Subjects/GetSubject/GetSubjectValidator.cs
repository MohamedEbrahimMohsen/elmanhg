using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Subjects.GetSubject;

public sealed class GetSubjectValidator : AbstractValidator<GetSubjectQuery>
{
    public GetSubjectValidator()
    {
        RuleFor(x => x.SubjectId).ValidateRequired(ErrorCodes.SubjectIdRequired);
    }
}
