using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Units.ReorderUnit;

public sealed class ReorderUnitValidator : AbstractValidator<ReorderUnitCommand>
{
    public ReorderUnitValidator()
    {
        RuleFor(x => x.SubjectId).ValidateRequired(ErrorCodes.SubjectIdRequired);
        RuleFor(x => x.UnitId).ValidateRequired(ErrorCodes.UnitIdRequired);
        RuleFor(x => x.Position).ValidateMin(1, ErrorCodes.UnitPositionInvalid);
    }
}
