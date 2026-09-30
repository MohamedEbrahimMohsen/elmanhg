using System.ComponentModel.DataAnnotations;

namespace Elmanhg.Application.Shared.Options;

public sealed class EssayGradingOptions
{
    public const string SectionName = "EssayGrading";

    public bool SweepEnabled { get; set; } = true;

    [Range(1, 3600)]
    public int SweepIntervalSeconds { get; set; } = 10;

    [Range(1, 100)]
    public int SweepBatchSize { get; set; } = 5;

    [Range(1, 10)]
    public int MaxAttempts { get; set; } = 4;

    [Range(1, 3600)]
    public int RetryBaseDelaySeconds { get; set; } = 30;

    [Range(typeof(decimal), "0", "1")]
    public decimal ReviewConfidenceThreshold { get; set; } = 0.7m;

    [Range(1, 100000)]
    public int ContextFieldMaxLength { get; set; } = 20000;
}
