using Elmanhg.Application.Exceptions;
using Elmanhg.Application.GradeReviews.ReviewEssayGrade;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Questions.Grading;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.GradeReviews.Shared;

public sealed class GradeReviewInputValidatorTests
{
    private static readonly GradeReviewOptions ReviewOptions = new();
    private readonly ReviewEssayGradeValidator _validator = new(Options.Create(ReviewOptions));

    [Fact]
    public void Validate_AcceptWithoutScore_Passes()
    {
        Validate(GradeReviewDecision.Accepted, null, null).Should().BeEmpty();
    }

    [Fact]
    public void Validate_OverrideWithScoreAndComment_Passes()
    {
        Validate(GradeReviewDecision.Overridden, 3.25m, "Clear definition.").Should().BeEmpty();
    }

    [Fact]
    public void Validate_UndefinedDecision_ReturnsDecisionInvalid()
    {
        Validate((GradeReviewDecision)9, null, null).Should().Equal(ErrorCodes.GradeReviewDecisionInvalid);
    }

    [Fact]
    public void Validate_OverrideWithoutScore_ReturnsScoreRequired()
    {
        Validate(GradeReviewDecision.Overridden, null, "Clear definition.").Should().Equal(ErrorCodes.GradeReviewScoreRequired);
    }

    [Fact]
    public void Validate_AcceptWithScore_ReturnsScoreNotAllowed()
    {
        Validate(GradeReviewDecision.Accepted, 2m, null).Should().Equal(ErrorCodes.GradeReviewScoreNotAllowed);
    }

    [Fact]
    public void Validate_NegativeScore_ReturnsScoreInvalid()
    {
        Validate(GradeReviewDecision.Overridden, -1m, "Clear definition.").Should().Equal(ErrorCodes.GradeReviewScoreInvalid);
    }

    [Fact]
    public void Validate_ThreeDecimalScore_ReturnsScoreInvalid()
    {
        Validate(GradeReviewDecision.Overridden, 1.234m, "Clear definition.").Should().Equal(ErrorCodes.GradeReviewScoreInvalid);
    }

    [Fact]
    public void Validate_OverrideWithBlankComment_ReturnsCommentRequired()
    {
        Validate(GradeReviewDecision.Overridden, 3m, "  ").Should().Equal(ErrorCodes.GradeReviewCommentRequired);
    }

    [Fact]
    public void Validate_CommentOverMax_ReturnsCommentTooLong()
    {
        Validate(GradeReviewDecision.Accepted, null, new string('a', ReviewOptions.CommentMaxLength + 1)).Should().Equal(ErrorCodes.GradeReviewCommentTooLong);
    }

    private List<string> Validate(GradeReviewDecision decision, decimal? score, string? comment)
    {
        return _validator.Validate(new ReviewEssayGradeCommand(Guid.NewGuid(), Guid.NewGuid(), decision, score, comment)).Errors
            .Select(x => x.ErrorCode)
            .ToList();
    }
}
