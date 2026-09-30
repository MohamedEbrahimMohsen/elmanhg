using Elmanhg.Application.GradeReviews.Shared;
using Elmanhg.Application.Shared.Authorization;
using MediatR;

namespace Elmanhg.Application.GradeReviews.GetMathStepGradeReview;

public sealed record GetMathStepGradeReviewQuery(Guid SubjectId, Guid MathStepGradeId) : IRequest<GradeReviewDetailResult>, ISubjectScopedRequest;
