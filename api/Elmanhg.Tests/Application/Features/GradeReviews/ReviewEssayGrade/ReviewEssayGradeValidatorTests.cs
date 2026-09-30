using Elmanhg.Application.Exceptions;
using Elmanhg.Application.GradeReviews.ReviewEssayGrade;
using Elmanhg.Application.Shared.Options;
using Elmanhg.Domain.Questions.Grading;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.GradeReviews.ReviewEssayGrade;

public sealed class ReviewEssayGradeValidatorTests
{
    private readonly ReviewEssayGradeValidator _validator = new(Options.Create(new GradeReviewOptions()));

    [Fact]
    public void Validate_EmptySubjectId_ReturnsSubjectIdRequired()
    {
        _validator.Validate(new ReviewEssayGradeCommand(Guid.Empty, Guid.NewGuid(), GradeReviewDecision.Accepted, null, null)).Errors.Select(x => x.ErrorCode).Should().Equal(ErrorCodes.SubjectIdRequired);
    }

    [Fact]
    public void Validate_EmptyEssayGradeId_ReturnsGradeReviewIdRequired()
    {
        _validator.Validate(new ReviewEssayGradeCommand(Guid.NewGuid(), Guid.Empty, GradeReviewDecision.Accepted, null, null)).Errors.Select(x => x.ErrorCode).Should().Equal(ErrorCodes.GradeReviewIdRequired);
    }
}
