using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Domain.Sessions.Exams;
using FluentValidation;

namespace Elmanhg.Application.Exams.Shared;

public sealed class MultiUnitExamSelectionValidator : AbstractValidator<MultiUnitExamSelection>
{
    public MultiUnitExamSelectionValidator()
    {
        RuleFor(x => x.SubjectId).ValidateRequired(ErrorCodes.SubjectIdRequired);
        RuleFor(x => x.UnitIds)
            .Must(list => list is not null && list.Count >= MultiUnitExamSizes.MinUnits)
            .WithErrorCode(ErrorCodes.MultiUnitExamUnitsTooFew)
            .Must(list => list is null || list.Distinct().Count() == list.Count)
            .WithErrorCode(ErrorCodes.MultiUnitExamUnitDuplicate);
        RuleForEach(x => x.UnitIds).ValidateRequired(ErrorCodes.UnitIdRequired);
        RuleFor(x => x.Size)
            .Must(MultiUnitExamSizes.IsAllowed)
            .WithErrorCode(ErrorCodes.MultiUnitExamSizeInvalid);
    }
}
