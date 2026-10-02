using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.SlaCalendars.UpdateExamPeriod;

public sealed class UpdateExamPeriodValidator : AbstractValidator<UpdateExamPeriodCommand>
{
    public UpdateExamPeriodValidator(IOptions<SlaCalendarOptions> slaCalendarOptions)
    {
        var options = slaCalendarOptions.Value;

        RuleFor(x => x.ExamPeriodId).ValidateRequired(ErrorCodes.ExamPeriodIdRequired);
        RuleFor(x => x.Name).ValidateRequired(ErrorCodes.ExamPeriodNameRequired).ValidateMaxLength(options.ExamPeriodNameMaxLength, ErrorCodes.ExamPeriodNameTooLong);
        RuleFor(x => x.StartDate).ValidateRequired(ErrorCodes.ExamPeriodStartDateRequired);
        RuleFor(x => x.EndDate).ValidateRequired(ErrorCodes.ExamPeriodEndDateRequired);
        RuleFor(x => x).Must(x => x.StartDate is null || x.EndDate is null || x.EndDate >= x.StartDate).WithErrorCode(ErrorCodes.ExamPeriodDateRangeInvalid);
        RuleFor(x => x).Must(x => x.StartDate is null || x.EndDate is null || x.EndDate < x.StartDate || x.EndDate.Value.DayNumber - x.StartDate.Value.DayNumber + 1 <= options.ExamPeriodMaxDays).WithErrorCode(ErrorCodes.ExamPeriodTooLong);
    }
}
