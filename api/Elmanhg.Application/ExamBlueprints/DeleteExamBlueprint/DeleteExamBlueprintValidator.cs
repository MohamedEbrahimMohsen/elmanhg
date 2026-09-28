using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.ExamBlueprints.DeleteExamBlueprint;

public sealed class DeleteExamBlueprintValidator : AbstractValidator<DeleteExamBlueprintCommand>
{
    public DeleteExamBlueprintValidator()
    {
        RuleFor(x => x.ExamBlueprintId).ValidateRequired(ErrorCodes.ExamBlueprintIdRequired);
    }
}
