using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Mastery.GetSubjectMastery;

public sealed class GetSubjectMasteryValidator : AbstractValidator<GetSubjectMasteryQuery>
{
    public GetSubjectMasteryValidator()
    {
        RuleFor(x => x.SubjectId).ValidateRequired(ErrorCodes.SubjectIdRequired);
    }
}
