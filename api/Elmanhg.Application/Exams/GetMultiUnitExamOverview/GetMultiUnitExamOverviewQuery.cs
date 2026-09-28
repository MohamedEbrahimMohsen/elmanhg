using Elmanhg.Application.Exams.Shared;
using MediatR;

namespace Elmanhg.Application.Exams.GetMultiUnitExamOverview;

public sealed record GetMultiUnitExamOverviewQuery(Guid SubjectId) : IRequest<MultiUnitExamOverviewResult>;
