using System.ComponentModel.DataAnnotations;

namespace Elmanhg.Application.Shared.Options;

public sealed class ExamBlueprintsOptions
{
    public const string SectionName = "ExamBlueprints";

    [Range(1, 500)]
    public int MaxQuestionCount { get; set; } = 100;

    [Range(1, 1440)]
    public int MaxTimeLimitMinutes { get; set; } = 300;
}
