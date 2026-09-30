using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using FluentValidation;

namespace Elmanhg.Application.GradeReviews.GetEssayGradeReview;

public sealed class GetEssayGradeReviewValidator : AbstractValidator<GetEssayGradeReviewQuery>
{
    public GetEssayGradeReviewValidator()
    {
        RuleFor(x => x.SubjectId).ValidateRequired(ErrorCodes.SubjectIdRequired);
        RuleFor(x => x.EssayGradeId).ValidateRequired(ErrorCodes.GradeReviewIdRequired);
    }
}
