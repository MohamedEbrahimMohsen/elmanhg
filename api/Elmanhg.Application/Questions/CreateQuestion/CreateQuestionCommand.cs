using Core.Auditing;
using Elmanhg.Application.Questions.Shared;
using MediatR;

namespace Elmanhg.Application.Questions.CreateQuestion;

public sealed record CreateQuestionCommand(Guid LessonId, QuestionFields Question) : IRequest<CreateQuestionResult>, IAuditableCommand
{
    public string AuditAction => "Question.Create";
    public string AuditResourceType => "Question";
    public Guid? AuditResourceId => null;
}
