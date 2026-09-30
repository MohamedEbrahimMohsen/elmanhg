namespace Elmanhg.Application.Dashboard.Shared;

public interface IDashboardRange
{
    DateOnly? From { get; }
    DateOnly? To { get; }
}
