using Elmanhg.Application.Exams.Shared;
using MediatR;

namespace Elmanhg.Application.Exams.GetUnitExamOverview;

public sealed record GetUnitExamOverviewQuery(Guid UnitId) : IRequest<UnitExamOverviewResult>;
