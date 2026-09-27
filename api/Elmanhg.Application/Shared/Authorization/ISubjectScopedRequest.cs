namespace Elmanhg.Application.Shared.Authorization;

public interface ISubjectScopedRequest
{
    Guid SubjectId { get; }
}
