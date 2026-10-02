using Core.Auditing;
using Elmanhg.Application.SlaCalendars.Shared;
using MediatR;

namespace Elmanhg.Application.SlaCalendars.UpdateExamPeriod;

public sealed record UpdateExamPeriodCommand(Guid ExamPeriodId, string? Name, DateOnly? StartDate, DateOnly? EndDate) : IRequest<ExamPeriodResult>, IAuditableCommand
{
    public string AuditAction => "ExamPeriod.Update";
    public string AuditResourceType => "ExamPeriod";
    public Guid? AuditResourceId => ExamPeriodId;
}
