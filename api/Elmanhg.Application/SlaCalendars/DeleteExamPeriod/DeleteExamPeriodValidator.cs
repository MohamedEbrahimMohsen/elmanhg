using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.SlaCalendars.DeleteExamPeriod;

public sealed class DeleteExamPeriodValidator : AbstractValidator<DeleteExamPeriodCommand>
{
    public DeleteExamPeriodValidator()
    {
        RuleFor(x => x.ExamPeriodId).ValidateRequired(ErrorCodes.ExamPeriodIdRequired);
    }
}
