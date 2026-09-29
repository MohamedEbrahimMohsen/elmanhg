using System.ComponentModel.DataAnnotations;

namespace Elmanhg.Application.Shared.Options;

public sealed class ExamsOptions
{
    public const string SectionName = "Exams";

    [Range(0, 600)]
    public int DeadlineGraceSeconds { get; set; } = 30;

    public bool AutoSubmitEnabled { get; set; } = true;

    [Range(5, 3600)]
    public int AutoSubmitIntervalSeconds { get; set; } = 60;

    [Range(1, 1000)]
    public int AutoSubmitBatchSize { get; set; } = 50;

    [Range(1, 20)]
    public int WeakestObjectiveCount { get; set; } = 3;

    public bool RequireAllLessonsOpened { get; set; }

    public TimeSpan DeadlineGrace => TimeSpan.FromSeconds(DeadlineGraceSeconds);
}
