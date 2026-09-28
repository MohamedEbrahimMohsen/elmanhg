using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Exams.GetUnitExamOverview;

public sealed class GetUnitExamOverviewValidator : AbstractValidator<GetUnitExamOverviewQuery>
{
    public GetUnitExamOverviewValidator()
    {
        RuleFor(x => x.UnitId).ValidateRequired(ErrorCodes.UnitIdRequired);
    }
}
