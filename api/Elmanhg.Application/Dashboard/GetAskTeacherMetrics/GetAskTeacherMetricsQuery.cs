using Elmanhg.Application.Dashboard.Shared;
using MediatR;

namespace Elmanhg.Application.Dashboard.GetAskTeacherMetrics;

public sealed record GetAskTeacherMetricsQuery(DateOnly? From, DateOnly? To, Guid? SubjectId) : IRequest<AskTeacherMetricsResult>, IDashboardRangeQuery
{
    public string CacheKey => DashboardCacheKey.For("ask-teacher", From, To, SubjectId);
}
