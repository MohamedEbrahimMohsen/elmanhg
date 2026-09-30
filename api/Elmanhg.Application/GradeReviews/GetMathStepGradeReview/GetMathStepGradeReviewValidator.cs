using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.GradeReviews.GetMathStepGradeReview;

public sealed class GetMathStepGradeReviewValidator : AbstractValidator<GetMathStepGradeReviewQuery>
{
    public GetMathStepGradeReviewValidator()
    {
        RuleFor(x => x.SubjectId).ValidateRequired(ErrorCodes.SubjectIdRequired);
        RuleFor(x => x.MathStepGradeId).ValidateRequired(ErrorCodes.GradeReviewIdRequired);
    }
}
