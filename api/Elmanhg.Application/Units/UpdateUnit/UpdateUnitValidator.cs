using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Units.UpdateUnit;

public sealed class UpdateUnitValidator : AbstractValidator<UpdateUnitCommand>
{
    public UpdateUnitValidator(IOptions<ContentOptions> contentOptions)
    {
        var options = contentOptions.Value;

        RuleFor(x => x.SubjectId).ValidateRequired(ErrorCodes.SubjectIdRequired);
        RuleFor(x => x.UnitId).ValidateRequired(ErrorCodes.UnitIdRequired);
        RuleFor(x => x.Name)
            .ValidateRequired(ErrorCodes.UnitNameRequired)
            .ValidateMaxLength(options.UnitNameMaxLength, ErrorCodes.UnitNameTooLong);
    }
}
