using Elmanhg.Domain.Subscriptions;
using System.ComponentModel.DataAnnotations;

namespace Elmanhg.Application.Shared.Options;

public sealed class SubscriptionsOptions
{
    public const string SectionName = "Subscriptions";

    // PRD §11.1: the Ask a Teacher add-on is sold monthly only.
    public const int AskTeacherPeriodMonths = 1;

    [Required, RegularExpression("^[A-Z]{3}$")]
    public string Currency { get; set; } = "EGP";

    [Range(0, 30)]
    public int GracePeriodDays { get; set; } = 3;

    [Range(0, 1000)]
    public int FreeDailyQuizQuestions { get; set; } = 10;

    [Range(0, 1000)]
    public int FreeDailyAvatarMessages { get; set; } = 5;

    [Range(0, 100)]
    public int FreeOpenLessonsPerUnit { get; set; } = 1;

    [Range(1, 10000)]
    public int BaseDailyAvatarMessages { get; set; } = 50;

    public Dictionary<BillingPeriod, PlanPriceOptions> BasePrices { get; set; } = [];

    [Range(1, 1000)]
    public int AskTeacherMonthlyQuestions { get; set; } = 20;

    [Range(1, 168)]
    public int AskTeacherReplySlaHours { get; set; } = 24;

    [Range(typeof(long), "1", "100000000")]
    public long AskTeacherMonthlyPriceMinor { get; set; }

    [Range(1, 100)]
    public int PaymentHistoryMaxPageSize { get; set; } = 50;

    public TimeSpan GracePeriod => TimeSpan.FromDays(GracePeriodDays);
}
