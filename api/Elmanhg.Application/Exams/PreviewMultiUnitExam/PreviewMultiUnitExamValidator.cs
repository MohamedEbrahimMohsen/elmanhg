using Elmanhg.Application.Exams.Shared;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Exams.PreviewMultiUnitExam;

public sealed class PreviewMultiUnitExamValidator : AbstractValidator<PreviewMultiUnitExamQuery>
{
    public PreviewMultiUnitExamValidator()
    {
        RuleFor(x => x.Selection)
            .NotNull()
            .WithErrorCode(ErrorCodes.MultiUnitExamUnitsTooFew)
            .SetValidator(new MultiUnitExamSelectionValidator());
    }
}
