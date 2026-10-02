using System.ComponentModel.DataAnnotations;

namespace Elmanhg.Infrastructure.AiService;

public sealed class AiServiceOptions
{
    public const string SectionName = "AiService";

    public AiServiceProvider Provider { get; set; } = AiServiceProvider.Fake;

    [Required]
    public string BaseUrl { get; set; } = "http://localhost:8000";

    public string ServiceToken { get; set; } = string.Empty;

    [Range(1, 60)]
    public int AttemptTimeoutSeconds { get; set; } = 45;

    [Range(1, 120)]
    public int TotalTimeoutSeconds { get; set; } = 50;

    [Range(1, 600)]
    public int TranscriptionTimeoutSeconds { get; set; } = 150;

    [Range(1, 600)]
    public int EssayGradingTimeoutSeconds { get; set; } = 100;

    [Range(1, 120)]
    public int MathCheckTimeoutSeconds { get; set; } = 15;

    [Range(1, 600)]
    public int MathStepGradingTimeoutSeconds { get; set; } = 100;

    [Range(1, 30)]
    public int ConfigurationTimeoutSeconds { get; set; } = 5;
}
