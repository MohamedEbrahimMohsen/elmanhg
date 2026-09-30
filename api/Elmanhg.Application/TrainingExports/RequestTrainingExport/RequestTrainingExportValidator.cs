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
        RuleFor(x => x).Must(x => x.From < x.To).WithErrorCode(ErrorCodes.TrainingExportDateRangeInvalid);
        RuleFor(x => x).Must(x => x.From >= x.To || x.To - x.From <= TimeSpan.FromDays(options.MaxRangeDays)).WithErrorCode(ErrorCodes.TrainingExportDateRangeTooWide);
    }
}
