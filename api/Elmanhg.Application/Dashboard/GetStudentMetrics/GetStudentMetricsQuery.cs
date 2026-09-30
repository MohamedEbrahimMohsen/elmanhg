using Elmanhg.Application.Dashboard.Shared;
using MediatR;

namespace Elmanhg.Application.Dashboard.GetStudentMetrics;

public sealed record GetStudentMetricsQuery(DateOnly? From, DateOnly? To) : IRequest<StudentMetricsResult>, IDashboardRangeQuery
{
    public string CacheKey => DashboardCacheKey.For("students", From, To, null);
}
