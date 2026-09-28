using Core.Auditing;

namespace Elmanhg.Application.Subjects.CreateSubject;

public sealed record CreateSubjectResult(Guid Id) : IAuditableResult
{
    Guid? IAuditableResult.AuditResourceId => Id;
}
