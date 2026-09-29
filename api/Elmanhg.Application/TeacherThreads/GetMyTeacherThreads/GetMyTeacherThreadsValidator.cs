using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.TeacherThreads.GetMyTeacherThreads;

public sealed class GetMyTeacherThreadsValidator : AbstractValidator<GetMyTeacherThreadsQuery>
{
    public GetMyTeacherThreadsValidator(IOptions<AskTeacherOptions> askTeacherOptions)
    {
        var options = askTeacherOptions.Value;

        RuleFor(x => x.PageNumber).ValidateMin(1, ErrorCodes.TeacherThreadPageNumberInvalid);
        RuleFor(x => x.PageSize).ValidateRange(1, options.ThreadListMaxPageSize, ErrorCodes.TeacherThreadPageSizeInvalid);
    }
}
