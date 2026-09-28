using Core.Auditing;
using MediatR;

namespace Elmanhg.Application.QuestionValidation.BulkApproveQuestions;

public sealed record BulkApproveQuestionsCommand(Guid ReviewSessionId, IList<Guid> QuestionIds) : IRequest<BulkApproveQuestionsResult>, IAuditableCommand
{
    public string AuditAction => "Question.BulkApprove";
    public string AuditResourceType => "ReviewSession";
    public Guid? AuditResourceId => ReviewSessionId;
}
