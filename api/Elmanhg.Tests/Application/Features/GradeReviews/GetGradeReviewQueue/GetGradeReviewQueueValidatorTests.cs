using Elmanhg.Application.Exceptions;
using Elmanhg.Application.GradeReviews.GetGradeReviewQueue;
using Elmanhg.Application.GradeReviews.Shared;
using Elmanhg.Application.Shared.Options;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Application.Features.GradeReviews.GetGradeReviewQueue;

public sealed class GetGradeReviewQueueValidatorTests
{
    private static readonly GradeReviewOptions ReviewOptions = new();
    private readonly GetGradeReviewQueueValidator _validator = new(Options.Create(ReviewOptions));

    [Fact]
    public void Validate_Valid_Passes()
    {
        _validator.Validate(new GetGradeReviewQueueQuery(Guid.NewGuid(), GradeReviewKind.MathSteps, 2, ReviewOptions.QueueMaxPageSize)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptySubjectId_ReturnsSubjectIdRequired()
    {
        Codes(new GetGradeReviewQueueQuery(Guid.Empty, GradeReviewKind.Essay)).Should().Equal(ErrorCodes.SubjectIdRequired);
    }

    [Fact]
    public void Validate_PageZero_ReturnsPageNumberInvalid()
    {
        Codes(new GetGradeReviewQueueQuery(Guid.NewGuid(), GradeReviewKind.Essay, 0)).Should().Equal(ErrorCodes.GradeReviewPageNumberInvalid);
    }

    [Fact]
    public void Validate_PageSizeOverMax_ReturnsPageSizeInvalid()
    {
        Codes(new GetGradeReviewQueueQuery(Guid.NewGuid(), GradeReviewKind.Essay, 1, ReviewOptions.QueueMaxPageSize + 1)).Should().Equal(ErrorCodes.GradeReviewPageSizeInvalid);
    }

    [Fact]
    public void Validate_UndefinedKind_ReturnsKindInvalid()
    {
        Codes(new GetGradeReviewQueueQuery(Guid.NewGuid(), (GradeReviewKind)9)).Should().Equal(ErrorCodes.GradeReviewKindInvalid);
    }

    [Fact]
    public void Validate_PageOffsetPastIntRange_FailsPageNumberInvalid()
    {
        Codes(new GetGradeReviewQueueQuery(Guid.NewGuid(), GradeReviewKind.Essay, int.MaxValue, 20)).Should().Contain(ErrorCodes.GradeReviewPageNumberInvalid);
    }

    private List<string> Codes(GetGradeReviewQueueQuery query)
    {
        return _validator.Validate(query).Errors
            .Select(x => x.ErrorCode)
            .ToList();
    }
}
