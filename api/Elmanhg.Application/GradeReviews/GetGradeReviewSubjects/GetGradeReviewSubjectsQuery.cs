using Elmanhg.Application.GradeReviews.Shared;
using MediatR;

namespace Elmanhg.Application.GradeReviews.GetGradeReviewSubjects;

public sealed record GetGradeReviewSubjectsQuery : IRequest<List<GradeReviewSubjectResult>>;
