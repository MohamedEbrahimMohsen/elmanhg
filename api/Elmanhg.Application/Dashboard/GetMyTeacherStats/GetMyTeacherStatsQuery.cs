using Elmanhg.Application.Dashboard.Shared;
using MediatR;

namespace Elmanhg.Application.Dashboard.GetMyTeacherStats;

public sealed record GetMyTeacherStatsQuery(DateOnly? From, DateOnly? To) : IRequest<MyTeacherStatsResult>, IDashboardRange;
