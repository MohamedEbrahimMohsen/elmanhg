using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Exams.GetMultiUnitExamOverview;

public sealed class GetMultiUnitExamOverviewValidator : AbstractValidator<GetMultiUnitExamOverviewQuery>
{
    public GetMultiUnitExamOverviewValidator()
    {
        RuleFor(x => x.SubjectId).ValidateRequired(ErrorCodes.SubjectIdRequired);
    }
}
