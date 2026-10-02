using Core.Auditing;
using Elmanhg.Application.SlaCalendars.Shared;
using MediatR;

namespace Elmanhg.Application.SlaCalendars.CreateExamPeriod;

public sealed record CreateExamPeriodCommand(string? Name, DateOnly? StartDate, DateOnly? EndDate) : IRequest<ExamPeriodResult>, IAuditableCommand
{
    public string AuditAction => "ExamPeriod.Create";
    public string AuditResourceType => "ExamPeriod";
    public Guid? AuditResourceId => null;
}
