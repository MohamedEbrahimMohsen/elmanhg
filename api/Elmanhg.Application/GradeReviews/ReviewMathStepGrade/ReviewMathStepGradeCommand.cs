using Core.Auditing;
using Elmanhg.Application.GradeReviews.Shared;
using Elmanhg.Application.Shared.Authorization;
using Elmanhg.Domain.Questions.Grading;
using MediatR;

namespace Elmanhg.Application.GradeReviews.ReviewMathStepGrade;

public sealed record ReviewMathStepGradeCommand(Guid SubjectId, Guid MathStepGradeId, GradeReviewDecision Decision, decimal? Score, string? Comment) : IRequest<GradeReviewDetailResult>, ISubjectScopedRequest, IGradeReviewInput, IAuditableCommand
{
    public string AuditAction => "MathStepGrade.Review";
    public string AuditResourceType => "MathStepGrade";
    public Guid? AuditResourceId => MathStepGradeId;
}
