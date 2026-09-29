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

    [Required]
    public string DailyQuotaTimeZone { get; set; } = "Africa/Cairo";

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

    [Range(0, 30)]
    public int RenewalWindowDays { get; set; } = 7;

    public bool LapseSweepEnabled { get; set; } = true;

    [Range(5, 86400)]
    public int LapseSweepIntervalSeconds { get; set; } = 300;

    [Range(1, 1000)]
    public int LapseSweepBatchSize { get; set; } = 100;

    [Range(1, 100)]
    public int AdminPaymentLogMaxPageSize { get; set; } = 100;

    [Range(1, 2000)]
    public int RefundReasonMaxLength { get; set; } = 500;

    [Range(1, 100)]
    public int PaymentLogReferenceMaxLength { get; set; } = 100;

    public TimeSpan GracePeriod => TimeSpan.FromDays(GracePeriodDays);

    public TimeSpan RenewalWindow => TimeSpan.FromDays(RenewalWindowDays);

    public PlanPriceOptions? PriceFor(SubscriptionPlan plan, BillingPeriod period) => plan switch
    {
        SubscriptionPlan.Base => BasePrices.GetValueOrDefault(period),
        SubscriptionPlan.AskTeacher when period == BillingPeriod.Monthly => new PlanPriceOptions { Months = AskTeacherPeriodMonths, AmountMinor = AskTeacherMonthlyPriceMinor },
        _ => null,
    };
}
