using Core.Auditing;

namespace Elmanhg.Application.Questions.CreateQuestion;

public sealed record CreateQuestionResult(Guid Id) : IAuditableResult
{
    Guid? IAuditableResult.AuditResourceId => Id;
}
