using Core.Validation.Extensions;
using Elmanhg.Application.ExamBlueprints.Shared;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.ExamBlueprints.SaveSubjectExamBlueprint;

public sealed class SaveSubjectExamBlueprintValidator : AbstractValidator<SaveSubjectExamBlueprintCommand>
{
    public SaveSubjectExamBlueprintValidator(IOptions<ExamBlueprintsOptions> examBlueprintsOptions)
    {
        RuleFor(x => x.SubjectId).ValidateRequired(ErrorCodes.SubjectIdRequired);
        RuleFor(x => x.Blueprint)
            .NotNull()
            .WithErrorCode(ErrorCodes.ExamBlueprintEmpty)
            .SetValidator(new ExamBlueprintInputValidator(examBlueprintsOptions));
    }
}
