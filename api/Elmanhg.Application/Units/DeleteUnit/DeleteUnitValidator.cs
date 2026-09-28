using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Units.DeleteUnit;

public sealed class DeleteUnitValidator : AbstractValidator<DeleteUnitCommand>
{
    public DeleteUnitValidator()
    {
        RuleFor(x => x.SubjectId).ValidateRequired(ErrorCodes.SubjectIdRequired);
        RuleFor(x => x.UnitId).ValidateRequired(ErrorCodes.UnitIdRequired);
    }
}
