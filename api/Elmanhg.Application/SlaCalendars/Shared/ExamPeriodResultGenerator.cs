using Elmanhg.Domain.SlaCalendars;

namespace Elmanhg.Application.SlaCalendars.Shared;

public static class ExamPeriodResultGenerator
{
    public static ExamPeriodResult Generate(ExamPeriod examPeriod) => new(examPeriod.Id, examPeriod.Name, examPeriod.StartDate, examPeriod.EndDate, examPeriod.CreationDate, examPeriod.UpdationDate);
}
