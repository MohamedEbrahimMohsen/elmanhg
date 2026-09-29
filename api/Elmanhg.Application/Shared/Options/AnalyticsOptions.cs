using System.ComponentModel.DataAnnotations;

namespace Elmanhg.Application.Shared.Options;

public sealed class AnalyticsOptions
{
    public const string SectionName = "Analytics";

    [Range(1, int.MaxValue)]
    public int FunnelEventPermitLimit { get; set; } = 60;

    [Range(1, int.MaxValue)]
    public int FunnelEventWindowSeconds { get; set; } = 60;
}
