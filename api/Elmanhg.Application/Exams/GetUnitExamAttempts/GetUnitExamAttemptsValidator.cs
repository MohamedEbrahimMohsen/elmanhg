using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Exams.GetUnitExamAttempts;

public sealed class GetUnitExamAttemptsValidator : AbstractValidator<GetUnitExamAttemptsQuery>
{
    public GetUnitExamAttemptsValidator()
    {
        RuleFor(x => x.UnitId).ValidateRequired(ErrorCodes.UnitIdRequired);
    }
}
