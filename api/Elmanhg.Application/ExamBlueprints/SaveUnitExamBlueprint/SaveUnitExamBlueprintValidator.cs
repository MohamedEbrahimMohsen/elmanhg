using Core.Validation.Extensions;
using Elmanhg.Application.ExamBlueprints.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.ExamBlueprints.SaveUnitExamBlueprint;

public sealed class SaveUnitExamBlueprintValidator : AbstractValidator<SaveUnitExamBlueprintCommand>
{
    public SaveUnitExamBlueprintValidator(IOptions<ExamBlueprintsOptions> examBlueprintsOptions)
    {
        RuleFor(x => x.UnitId).ValidateRequired(ErrorCodes.UnitIdRequired);
        RuleFor(x => x.Blueprint)
            .NotNull()
            .WithErrorCode(ErrorCodes.ExamBlueprintEmpty)
            .SetValidator(new ExamBlueprintInputValidator(examBlueprintsOptions));
    }
}
