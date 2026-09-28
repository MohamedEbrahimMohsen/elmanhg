using Core.Auditing;
using Elmanhg.Application.Questions.Shared;
using MediatR;

namespace Elmanhg.Application.Questions.ResubmitQuestion;

public sealed record ResubmitQuestionCommand(Guid QuestionId, QuestionFields Question) : IRequest, IAuditableCommand
{
    public string AuditAction => "Question.Resubmit";
    public string AuditResourceType => "Question";
    public Guid? AuditResourceId => QuestionId;
}
