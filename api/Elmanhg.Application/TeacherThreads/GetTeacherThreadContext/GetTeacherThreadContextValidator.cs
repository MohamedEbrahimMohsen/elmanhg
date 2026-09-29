using Elmanhg.Application.Exceptions;
using Elmanhg.Application.TeacherThreads.Shared;
using FluentValidation;

namespace Elmanhg.Application.TeacherThreads.GetTeacherThreadContext;

public sealed class GetTeacherThreadContextValidator : AbstractValidator<GetTeacherThreadContextQuery>
{
    public GetTeacherThreadContextValidator()
    {
        RuleFor(x => x)
            .Must(x => TeacherThreadContextRules.HasExactlyOne(x.LessonId, x.QuestionId, x.AttemptId))
            .WithErrorCode(ErrorCodes.TeacherThreadContextInvalid);
    }
}
