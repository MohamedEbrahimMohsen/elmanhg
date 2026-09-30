using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.MathStepGrading.GetMathStepGrade;

public sealed class GetMathStepGradeValidator : AbstractValidator<GetMathStepGradeQuery>
{
    public GetMathStepGradeValidator()
    {
        RuleFor(x => x.SessionId).ValidateRequired(ErrorCodes.SessionIdRequired);
        RuleFor(x => x.QuestionId).ValidateRequired(ErrorCodes.QuestionIdRequired);
    }
}
