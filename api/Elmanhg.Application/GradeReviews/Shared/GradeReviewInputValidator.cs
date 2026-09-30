using Core.Validation.Extensions;
using Elmanhg.Application.Exceptions;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Questions.Grading;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Elmanhg.Application.GradeReviews.Shared;

public sealed class GradeReviewInputValidator : AbstractValidator<IGradeReviewInput>
{
    public GradeReviewInputValidator(IOptions<GradeReviewOptions> gradeReviewOptions)
    {
        var options = gradeReviewOptions.Value;

        RuleFor(x => x.Decision)
            .IsInEnum()
            .WithErrorCode(ErrorCodes.GradeReviewDecisionInvalid);
        RuleFor(x => x.Score)
            .ValidateRequired(ErrorCodes.GradeReviewScoreRequired)
            .When(x => x.Decision == GradeReviewDecision.Overridden);
        RuleFor(x => x.Score)
            .Null()
            .WithErrorCode(ErrorCodes.GradeReviewScoreNotAllowed)
            .When(x => x.Decision == GradeReviewDecision.Accepted);
        RuleFor(x => x.Score!.Value)
            .ValidateNonNegative(ErrorCodes.GradeReviewScoreInvalid)
            .Must(x => decimal.Round(x, 2) == x)
            .WithErrorCode(ErrorCodes.GradeReviewScoreInvalid)
            .When(x => x.Score.HasValue)
            .OverridePropertyName(nameof(IGradeReviewInput.Score));
        RuleFor(x => x.Comment)
            .Must(x => !string.IsNullOrWhiteSpace(x))
            .WithErrorCode(ErrorCodes.GradeReviewCommentRequired)
            .When(x => x.Decision == GradeReviewDecision.Overridden);
        RuleFor(x => x.Comment).ValidateMaxLength(options.CommentMaxLength, ErrorCodes.GradeReviewCommentTooLong);
    }
}
