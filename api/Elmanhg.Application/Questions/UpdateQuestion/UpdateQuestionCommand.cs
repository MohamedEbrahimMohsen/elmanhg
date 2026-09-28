using Core.Auditing;
using Elmanhg.Application.Questions.Shared;
using MediatR;

namespace Elmanhg.Application.Questions.UpdateQuestion;

public sealed record UpdateQuestionCommand(Guid QuestionId, QuestionFields Question) : IRequest, IAuditableCommand
{
    public string AuditAction => "Question.Update";
    public string AuditResourceType => "Question";
    public Guid? AuditResourceId => QuestionId;
}
