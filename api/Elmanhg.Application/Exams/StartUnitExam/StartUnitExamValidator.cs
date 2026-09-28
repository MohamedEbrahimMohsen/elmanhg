using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Exams.StartUnitExam;

public sealed class StartUnitExamValidator : AbstractValidator<StartUnitExamCommand>
{
    public StartUnitExamValidator()
    {
        RuleFor(x => x.UnitId).ValidateRequired(ErrorCodes.UnitIdRequired);
    }
}
