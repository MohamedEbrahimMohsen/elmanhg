using Core.Auditing;
using Elmanhg.Application.GradeReviews.Shared;
using Elmanhg.Application.Shared.Authorization;
using Elmanhg.Domain.Questions.Grading;
using MediatR;

namespace Elmanhg.Application.GradeReviews.ReviewEssayGrade;

public sealed record ReviewEssayGradeCommand(Guid SubjectId, Guid EssayGradeId, GradeReviewDecision Decision, decimal? Score, string? Comment) : IRequest<GradeReviewDetailResult>, ISubjectScopedRequest, IGradeReviewInput, IAuditableCommand
{
    public string AuditAction => "EssayGrade.Review";
    public string AuditResourceType => "EssayGrade";
    public Guid? AuditResourceId => EssayGradeId;
}
