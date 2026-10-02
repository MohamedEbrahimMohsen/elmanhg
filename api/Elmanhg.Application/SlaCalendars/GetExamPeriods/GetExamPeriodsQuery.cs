using Elmanhg.Application.SlaCalendars.Shared;
using MediatR;

namespace Elmanhg.Application.SlaCalendars.GetExamPeriods;

public sealed record GetExamPeriodsQuery : IRequest<List<ExamPeriodResult>>;
