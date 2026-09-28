using Core.Auditing;
using MediatR;

namespace Elmanhg.Application.QuestionValidation.RejectQuestion;

public sealed record RejectQuestionCommand(Guid QuestionId, int Version, string? Reason) : IRequest, IAuditableCommand
{
    public string AuditAction => "Question.Reject";
    public string AuditResourceType => "Question";
    public Guid? AuditResourceId => QuestionId;
}
