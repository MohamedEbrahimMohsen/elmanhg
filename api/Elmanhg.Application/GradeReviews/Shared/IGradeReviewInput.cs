using Elmanhg.Domain.Questions.Grading;

namespace Elmanhg.Application.GradeReviews.Shared;

public interface IGradeReviewInput
{
    GradeReviewDecision Decision { get; }
    decimal? Score { get; }
    string? Comment { get; }
}
