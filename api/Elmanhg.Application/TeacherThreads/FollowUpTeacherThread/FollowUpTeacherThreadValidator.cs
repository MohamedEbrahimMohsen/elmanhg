using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.TeacherThreads.FollowUpTeacherThread;

public sealed class FollowUpTeacherThreadValidator : AbstractValidator<FollowUpTeacherThreadCommand>
{
    public FollowUpTeacherThreadValidator(IOptions<AskTeacherOptions> askTeacherOptions)
    {
        var options = askTeacherOptions.Value;

        RuleFor(x => x.Text)
            .Cascade(CascadeMode.Stop)
            .ValidateRequired(ErrorCodes.TeacherThreadTextRequired)
            .ValidateMaxLength(options.QuestionTextMaxLength, ErrorCodes.TeacherThreadTextTooLong);
    }
}
