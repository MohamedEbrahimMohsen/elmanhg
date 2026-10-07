using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.TrainingExports.GetTrainingExports;

public sealed class GetTrainingExportsValidator : AbstractValidator<GetTrainingExportsQuery>
{
    public GetTrainingExportsValidator(IOptions<TrainingExportsOptions> trainingExportsOptions)
    {
        var options = trainingExportsOptions.Value;

        RuleFor(x => x).ValidatePaging(x => x.PageNumber, x => x.PageSize, options.ListMaxPageSize, ErrorCodes.TrainingExportPageNumberInvalid, ErrorCodes.TrainingExportPageSizeInvalid);
    }
}
