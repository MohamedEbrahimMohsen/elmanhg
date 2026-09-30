using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.EssayGrading.GetEssayGrade;

public sealed class GetEssayGradeValidator : AbstractValidator<GetEssayGradeQuery>
{
    public GetEssayGradeValidator()
    {
        RuleFor(x => x.SessionId).ValidateRequired(ErrorCodes.SessionIdRequired);
        RuleFor(x => x.QuestionId).ValidateRequired(ErrorCodes.QuestionIdRequired);
    }
}
