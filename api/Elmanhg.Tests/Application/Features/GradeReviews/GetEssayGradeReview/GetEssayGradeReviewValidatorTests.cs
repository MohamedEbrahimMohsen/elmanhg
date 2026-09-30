using Elmanhg.Application.Exceptions;
using Elmanhg.Application.GradeReviews.GetEssayGradeReview;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.GradeReviews.GetEssayGradeReview;

public sealed class GetEssayGradeReviewValidatorTests
{
    private readonly GetEssayGradeReviewValidator _validator = new();

    [Fact]
    public void Validate_Valid_Passes()
    {
        _validator.Validate(new GetEssayGradeReviewQuery(Guid.NewGuid(), Guid.NewGuid())).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptySubjectId_ReturnsSubjectIdRequired()
    {
        _validator.Validate(new GetEssayGradeReviewQuery(Guid.Empty, Guid.NewGuid())).Errors.Select(x => x.ErrorCode).Should().Equal(ErrorCodes.SubjectIdRequired);
    }

    [Fact]
    public void Validate_EmptyId_ReturnsGradeReviewIdRequired()
    {
        _validator.Validate(new GetEssayGradeReviewQuery(Guid.NewGuid(), Guid.Empty)).Errors.Select(x => x.ErrorCode).Should().Equal(ErrorCodes.GradeReviewIdRequired);
    }
}
