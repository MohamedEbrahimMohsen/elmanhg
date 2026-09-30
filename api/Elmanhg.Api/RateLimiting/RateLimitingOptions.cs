using System.ComponentModel.DataAnnotations;

namespace Elmanhg.Api.RateLimiting;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    [Range(1, int.MaxValue)]
    public int AuthRefreshPermitLimit { get; set; } = 60;

    [Range(1, int.MaxValue)]
    public int AuthRefreshWindowSeconds { get; set; } = 60;

    [Range(1, int.MaxValue)]
    public int PublicReadPermitLimit { get; set; } = 300;

    [Range(1, int.MaxValue)]
    public int PublicReadWindowSeconds { get; set; } = 60;

    [Range(1, int.MaxValue)]
    public int PaymentWebhookPermitLimit { get; set; } = 300;

    [Range(1, int.MaxValue)]
    public int PaymentWebhookWindowSeconds { get; set; } = 60;

    [Range(1, int.MaxValue)]
    public int AvatarMessagePermitLimit { get; set; } = 20;

    [Range(1, int.MaxValue)]
    public int AvatarMessageWindowSeconds { get; set; } = 60;

    [Range(1, int.MaxValue)]
    public int AskTeacherSubmissionPermitLimit { get; set; } = 10;

    [Range(1, int.MaxValue)]
    public int AskTeacherSubmissionWindowSeconds { get; set; } = 600;

    [Range(1, int.MaxValue)]
    public int StudentConcurrentRequestLimit { get; set; } = 1;
}
