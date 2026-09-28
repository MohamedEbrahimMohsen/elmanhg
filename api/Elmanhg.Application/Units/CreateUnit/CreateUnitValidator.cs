using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Units.CreateUnit;

public sealed class CreateUnitValidator : AbstractValidator<CreateUnitCommand>
{
    public CreateUnitValidator(IOptions<ContentOptions> contentOptions)
    {
        var options = contentOptions.Value;

        RuleFor(x => x.SubjectId).ValidateRequired(ErrorCodes.SubjectIdRequired);
        RuleFor(x => x.Name)
            .ValidateRequired(ErrorCodes.UnitNameRequired)
            .ValidateMaxLength(options.UnitNameMaxLength, ErrorCodes.UnitNameTooLong);
    }
}
