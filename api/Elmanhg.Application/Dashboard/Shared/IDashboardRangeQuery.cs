namespace Elmanhg.Application.Dashboard.Shared;

public interface IDashboardRangeQuery : IDashboardQuery
{
    DateOnly? From { get; }
    DateOnly? To { get; }
}
