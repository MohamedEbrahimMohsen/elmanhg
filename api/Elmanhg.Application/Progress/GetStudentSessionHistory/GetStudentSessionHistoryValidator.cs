using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.Progress.GetStudentSessionHistory;

public sealed class GetStudentSessionHistoryValidator : AbstractValidator<GetStudentSessionHistoryQuery>
{
    public GetStudentSessionHistoryValidator(IOptions<ProgressOptions> progressOptions)
    {
        var options = progressOptions.Value;

        RuleFor(x => x.StudentId).ValidateRequired(ErrorCodes.StudentIdRequired);
        RuleFor(x => x.PageNumber).ValidateMin(1, ErrorCodes.SessionHistoryPageNumberInvalid);
        RuleFor(x => x.PageSize).ValidateRange(1, options.HistoryMaxPageSize, ErrorCodes.SessionHistoryPageSizeInvalid);
        RuleFor(x => x.Kind)
            .IsInEnum()
            .WithErrorCode(ErrorCodes.SessionHistoryKindInvalid);
    }
}
