using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Progress.GetSessionHistory;

public sealed class GetSessionHistoryValidator : AbstractValidator<GetSessionHistoryQuery>
{
    public GetSessionHistoryValidator(IOptions<ProgressOptions> progressOptions)
    {
        var options = progressOptions.Value;

        RuleFor(x => x).ValidatePaging(x => x.PageNumber, x => x.PageSize, options.HistoryMaxPageSize, ErrorCodes.SessionHistoryPageNumberInvalid, ErrorCodes.SessionHistoryPageSizeInvalid);
        RuleFor(x => x.Kind)
            .IsInEnum()
            .WithErrorCode(ErrorCodes.SessionHistoryKindInvalid);
    }
}
