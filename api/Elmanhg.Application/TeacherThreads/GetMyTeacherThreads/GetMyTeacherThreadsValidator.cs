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

        RuleFor(x => x).ValidatePaging(x => x.PageNumber, x => x.PageSize, options.ThreadListMaxPageSize, ErrorCodes.TeacherThreadPageNumberInvalid, ErrorCodes.TeacherThreadPageSizeInvalid);
    }
}
