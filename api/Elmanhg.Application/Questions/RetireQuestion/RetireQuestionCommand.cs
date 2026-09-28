using Core.Auditing;
using MediatR;

namespace Elmanhg.Application.Questions.RetireQuestion;

public sealed record RetireQuestionCommand(Guid QuestionId) : IRequest, IAuditableCommand
{
    public string AuditAction => "Question.Retire";
    public string AuditResourceType => "Question";
    public Guid? AuditResourceId => QuestionId;
}
