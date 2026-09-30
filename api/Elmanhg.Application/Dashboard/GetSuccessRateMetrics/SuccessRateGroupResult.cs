namespace Elmanhg.Application.Dashboard.GetSuccessRateMetrics;

public sealed record SuccessRateGroupResult(Guid Id, string Name, Guid? ParentId, int Attempts, int Correct, decimal? Rate);
