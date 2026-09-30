using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.GradeReviews.Shared;
using Elmanhg.Application.Shared.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.GradeReviews.ReviewEssayGrade;

public sealed class ReviewEssayGradeValidator : AbstractValidator<ReviewEssayGradeCommand>
{
    public ReviewEssayGradeValidator(IOptions<GradeReviewOptions> gradeReviewOptions)
    {
        RuleFor(x => x.SubjectId).ValidateRequired(ErrorCodes.SubjectIdRequired);
        RuleFor(x => x.EssayGradeId).ValidateRequired(ErrorCodes.GradeReviewIdRequired);
        Include(new GradeReviewInputValidator(gradeReviewOptions));
    }
}
