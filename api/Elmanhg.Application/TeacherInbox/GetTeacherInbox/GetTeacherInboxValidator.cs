using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.TeacherInbox.GetTeacherInbox;

public sealed class GetTeacherInboxValidator : AbstractValidator<GetTeacherInboxQuery>
{
    public GetTeacherInboxValidator(IOptions<AskTeacherOptions> askTeacherOptions)
    {
        var options = askTeacherOptions.Value;

        RuleFor(x => x.Filter).IsInEnum().WithErrorCode(ErrorCodes.TeacherInboxFilterInvalid);
        RuleFor(x => x).ValidatePaging(x => x.PageNumber, x => x.PageSize, options.ThreadListMaxPageSize, ErrorCodes.TeacherThreadPageNumberInvalid, ErrorCodes.TeacherThreadPageSizeInvalid);
    }
}
