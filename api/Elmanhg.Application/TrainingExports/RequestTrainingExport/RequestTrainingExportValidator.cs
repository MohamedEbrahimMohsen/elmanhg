using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.TrainingExports.RequestTrainingExport;

public sealed class RequestTrainingExportValidator : AbstractValidator<RequestTrainingExportCommand>
{
    public RequestTrainingExportValidator(IOptions<TrainingExportsOptions> trainingExportsOptions)
    {
        var options = trainingExportsOptions.Value;

        RuleFor(x => x.Source).IsInEnum().WithErrorCode(ErrorCodes.TrainingExportSourceInvalid);
        RuleFor(x => x).ValidateDateRange(x => x.From, x => x.To, ErrorCodes.TrainingExportDateRangeInvalid, TimeSpan.FromDays(options.MaxRangeDays), ErrorCodes.TrainingExportDateRangeTooWide);
    }
}
