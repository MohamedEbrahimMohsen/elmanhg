using Core.DDD.Models;
using Elmanhg.Application.GradeReviews.Shared;
using Elmanhg.Application.Shared.Authorization;
using MediatR;

namespace Elmanhg.Application.GradeReviews.GetGradeReviewQueue;

public sealed record GetGradeReviewQueueQuery(Guid SubjectId, GradeReviewKind Kind, int PageNumber = 1, int PageSize = 20) : IRequest<PageData<GradeReviewItemResult>>, ISubjectScopedRequest;
