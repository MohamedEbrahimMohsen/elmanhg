using Core.Auditing;
using Elmanhg.Domain.Questions;
using MediatR;

namespace Elmanhg.Application.QuestionValidation.ApproveQuestion;

public sealed record ApproveQuestionCommand(Guid QuestionId, int Version, QuestionDifficulty? Difficulty) : IRequest, IAuditableCommand
{
    public string AuditAction => "Question.Approve";
    public string AuditResourceType => "Question";
    public Guid? AuditResourceId => QuestionId;
}
