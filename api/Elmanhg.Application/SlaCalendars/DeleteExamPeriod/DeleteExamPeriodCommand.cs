using Core.Auditing;
using MediatR;

namespace Elmanhg.Application.SlaCalendars.DeleteExamPeriod;

public sealed record DeleteExamPeriodCommand(Guid ExamPeriodId) : IRequest, IAuditableCommand
{
    public string AuditAction => "ExamPeriod.Delete";
    public string AuditResourceType => "ExamPeriod";
    public Guid? AuditResourceId => ExamPeriodId;
}
