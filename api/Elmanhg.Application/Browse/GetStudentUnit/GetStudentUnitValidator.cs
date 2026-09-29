using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Browse.GetStudentUnit;

public sealed class GetStudentUnitValidator : AbstractValidator<GetStudentUnitQuery>
{
    public GetStudentUnitValidator()
    {
        RuleFor(x => x.UnitId).ValidateRequired(ErrorCodes.UnitIdRequired);
    }
}
