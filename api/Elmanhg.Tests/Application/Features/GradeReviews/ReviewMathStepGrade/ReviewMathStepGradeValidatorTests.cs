using Elmanhg.Application.Exceptions;
using Elmanhg.Application.GradeReviews.ReviewMathStepGrade;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Questions.Grading;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.GradeReviews.ReviewMathStepGrade;

public sealed class ReviewMathStepGradeValidatorTests
{
    private readonly ReviewMathStepGradeValidator _validator = new(Options.Create(new GradeReviewOptions()));

    [Fact]
    public void Validate_EmptySubjectId_ReturnsSubjectIdRequired()
    {
        _validator.Validate(new ReviewMathStepGradeCommand(Guid.Empty, Guid.NewGuid(), GradeReviewDecision.Accepted, null, null)).Errors.Select(x => x.ErrorCode).Should().Equal(ErrorCodes.SubjectIdRequired);
    }

    [Fact]
    public void Validate_EmptyMathStepGradeId_ReturnsGradeReviewIdRequired()
    {
        _validator.Validate(new ReviewMathStepGradeCommand(Guid.NewGuid(), Guid.Empty, GradeReviewDecision.Accepted, null, null)).Errors.Select(x => x.ErrorCode).Should().Equal(ErrorCodes.GradeReviewIdRequired);
    }

    [Fact]
    public void Validate_OverrideWithoutScore_ReturnsScoreRequired()
    {
        _validator.Validate(new ReviewMathStepGradeCommand(Guid.NewGuid(), Guid.NewGuid(), GradeReviewDecision.Overridden, null, "Correct method.")).Errors.Select(x => x.ErrorCode).Should().Equal(ErrorCodes.GradeReviewScoreRequired);
    }
}
