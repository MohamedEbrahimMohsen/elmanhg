using Elmanhg.Domain.Questions.Grading;

namespace Elmanhg.Api.Controllers.GradeReviews;

public sealed record ReviewGradeRequest(GradeReviewDecision Decision, decimal? Score, string? Comment);
