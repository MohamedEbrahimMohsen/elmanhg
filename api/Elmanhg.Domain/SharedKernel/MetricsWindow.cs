namespace Elmanhg.Domain.SharedKernel;

public sealed record MetricsWindow(DateTimeOffset Start, DateTimeOffset End, string TimeZone);
