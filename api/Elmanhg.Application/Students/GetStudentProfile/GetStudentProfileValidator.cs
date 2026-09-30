using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.Students.GetStudentProfile;

public sealed class GetStudentProfileValidator : AbstractValidator<GetStudentProfileQuery>
{
    public GetStudentProfileValidator()
    {
        RuleFor(x => x.StudentId).ValidateRequired(ErrorCodes.StudentIdRequired);
    }
}
