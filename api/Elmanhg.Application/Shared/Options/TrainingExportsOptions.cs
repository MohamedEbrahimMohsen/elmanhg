using System.ComponentModel.DataAnnotations;

namespace Elmanhg.Application.Shared.Options;

public sealed class TrainingExportsOptions
{
    public const string SectionName = "TrainingExports";

    public bool SweepEnabled { get; set; } = true;

    [Range(1, 3600)]
    public int SweepIntervalSeconds { get; set; } = 15;

    [Range(1, 20)]
    public int SweepBatchSize { get; set; } = 2;

    [Range(1, 10)]
    public int MaxAttempts { get; set; } = 3;

    [Range(1, 3600)]
    public int RetryBaseDelaySeconds { get; set; } = 60;

    [Range(1, 1440)]
    public int RunLeaseMinutes { get; set; } = 30;

    [Range(10, 5000)]
    public int ReadBatchSize { get; set; } = 500;

    [Range(1, 3660)]
    public int MaxRangeDays { get; set; } = 366;

    [Range(1, 100)]
    public int ListMaxPageSize { get; set; } = 50;

    [Range(1, 365)]
    public int RetentionDays { get; set; } = 7;

    public bool RetentionSweepEnabled { get; set; } = true;

    [Range(1, 86400)]
    public int RetentionSweepIntervalSeconds { get; set; } = 3600;

    [Range(1, 100)]
    public int RetentionSweepBatchSize { get; set; } = 20;

    public TimeSpan Retention => TimeSpan.FromDays(RetentionDays);

    public TimeSpan RunLease => TimeSpan.FromMinutes(RunLeaseMinutes);
}
