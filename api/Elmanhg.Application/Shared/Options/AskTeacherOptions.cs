using System.ComponentModel.DataAnnotations;

namespace Elmanhg.Application.Shared.Options;

public sealed class AskTeacherOptions
{
    public const string SectionName = "AskTeacher";

    [Range(1, 20000)]
    public int QuestionTextMaxLength { get; set; } = 2000;

    [Range(1, 20)]
    public int ImageMaxSizeInMb { get; set; } = 5;

    [Range(1, 100)]
    public int ThreadListMaxPageSize { get; set; } = 50;

    [Range(1, 20000)]
    public int ReplyTextMaxLength { get; set; } = 4000;

    [Range(1, 25)]
    public int VoiceMaxSizeInMb { get; set; } = 5;

    [Range(10, 600)]
    public int VoiceMaxDurationSeconds { get; set; } = 180;

    [Required]
    [RegularExpression("^[a-z]{2}$")]
    public string TranscriptionLanguage { get; set; } = "ar";

    public bool TranscriptionSweepEnabled { get; set; } = true;

    [Range(1, 3600)]
    public int TranscriptionSweepIntervalSeconds { get; set; } = 5;

    [Range(1, 100)]
    public int TranscriptionSweepBatchSize { get; set; } = 5;

    [Range(1, 10)]
    public int TranscriptionMaxAttempts { get; set; } = 4;

    [Range(1, 3600)]
    public int TranscriptionRetryBaseDelaySeconds { get; set; } = 15;

    public bool SlaSweepEnabled { get; set; } = true;

    [Range(1, 3600)]
    public int SlaSweepIntervalSeconds { get; set; } = 60;

    [Range(1, 500)]
    public int SlaSweepBatchSize { get; set; } = 50;

    [Range(1, 168)]
    public int FirstReminderAfterHours { get; set; } = 12;

    [Range(1, 168)]
    public int SecondReminderAfterHours { get; set; } = 20;

    [Range(1, 100)]
    public int ReminderListMaxCount { get; set; } = 20;
}
