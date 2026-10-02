using Core.Auditing;

namespace Elmanhg.Application.SlaCalendars.Shared;

public sealed record ExamPeriodResult(Guid Id, string Name, DateOnly StartDate, DateOnly EndDate, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt) : IAuditableResult
{
    Guid? IAuditableResult.AuditResourceId => Id;
}
