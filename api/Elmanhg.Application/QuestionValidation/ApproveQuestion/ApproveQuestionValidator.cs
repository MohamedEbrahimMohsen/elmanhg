using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.QuestionValidation.ApproveQuestion;

public sealed class ApproveQuestionValidator : AbstractValidator<ApproveQuestionCommand>
{
    public ApproveQuestionValidator()
    {
        RuleFor(x => x.QuestionId).ValidateRequired(ErrorCodes.QuestionIdRequired);
        RuleFor(x => x.Version).ValidateMin(1, ErrorCodes.QuestionVersionInvalid);
        RuleFor(x => x.Difficulty)
            .IsInEnum()
            .WithErrorCode(ErrorCodes.QuestionDifficultyInvalid);
    }
}
