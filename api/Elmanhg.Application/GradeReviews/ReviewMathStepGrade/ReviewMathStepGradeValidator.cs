using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.GradeReviews.Shared;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.GradeReviews.ReviewMathStepGrade;

public sealed class ReviewMathStepGradeValidator : AbstractValidator<ReviewMathStepGradeCommand>
{
    public ReviewMathStepGradeValidator(IOptions<GradeReviewOptions> gradeReviewOptions)
    {
        RuleFor(x => x.SubjectId).ValidateRequired(ErrorCodes.SubjectIdRequired);
        RuleFor(x => x.MathStepGradeId).ValidateRequired(ErrorCodes.GradeReviewIdRequired);
        Include(new GradeReviewInputValidator(gradeReviewOptions));
    }
}
