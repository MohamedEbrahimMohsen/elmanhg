using Elmanhg.Application.GradeReviews.Shared;
using Elmanhg.Application.Shared.Authorization;
using MediatR;

namespace Elmanhg.Application.GradeReviews.GetEssayGradeReview;

public sealed record GetEssayGradeReviewQuery(Guid SubjectId, Guid EssayGradeId) : IRequest<GradeReviewDetailResult>, ISubjectScopedRequest;
