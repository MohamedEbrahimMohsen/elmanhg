using Elmanhg.Application.Exams.Shared;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Exams.StartMultiUnitExam;

public sealed class StartMultiUnitExamValidator : AbstractValidator<StartMultiUnitExamCommand>
{
    public StartMultiUnitExamValidator()
    {
        RuleFor(x => x.Selection)
            .NotNull()
            .WithErrorCode(ErrorCodes.MultiUnitExamUnitsTooFew)
            .SetValidator(new MultiUnitExamSelectionValidator());
    }
}
