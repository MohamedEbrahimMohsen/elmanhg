using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.TeacherInbox.ReplyToTeacherThread;

public sealed class ReplyToTeacherThreadValidator : AbstractValidator<ReplyToTeacherThreadCommand>
{
    public ReplyToTeacherThreadValidator(IOptions<AskTeacherOptions> askTeacherOptions)
    {
        var options = askTeacherOptions.Value;

        RuleFor(x => x.Text)
            .Cascade(CascadeMode.Stop)
            .ValidateRequired(ErrorCodes.TeacherThreadReplyTextRequired)
            .ValidateMaxLength(options.ReplyTextMaxLength, ErrorCodes.TeacherThreadReplyTextTooLong);
    }
}
