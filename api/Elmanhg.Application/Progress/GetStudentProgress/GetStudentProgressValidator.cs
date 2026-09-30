using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Progress.GetStudentProgress;

public sealed class GetStudentProgressValidator : AbstractValidator<GetStudentProgressQuery>
{
    public GetStudentProgressValidator()
    {
        RuleFor(x => x.StudentId).ValidateRequired(ErrorCodes.StudentIdRequired);
    }
}
