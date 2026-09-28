using System.ComponentModel.DataAnnotations;

namespace Elmanhg.Application.Shared.Options;

public sealed class QuestionValidationOptions
{
    public const string SectionName = "QuestionValidation";

    [Range(1, 1000)]
    public int QueueMaxPageSize { get; set; } = 100;

    [Range(1, 3650)]
    public int QueueMaxAgeDays { get; set; } = 365;

    [Range(1, 10000)]
    public int RejectionReasonMaxLength { get; set; } = 1000;

    [Range(1, 500)]
    public int BulkApproveMaxCount { get; set; } = 50;

    [Range(1, 1440)]
    public int ReviewSessionLifetimeMinutes { get; set; } = 480;
}
