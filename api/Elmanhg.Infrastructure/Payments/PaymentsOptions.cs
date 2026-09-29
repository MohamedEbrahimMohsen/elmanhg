using Elmanhg.Infrastructure.Payments.Paymob;
using System.ComponentModel.DataAnnotations;

namespace Elmanhg.Infrastructure.Payments;

public sealed class PaymentsOptions
{
    public const string SectionName = "Payments";

    public PaymentProvider Provider { get; set; } = PaymentProvider.Fake;

    [Required, RegularExpression("^/[A-Za-z0-9/_-]*$")]
    public string FakeCheckoutPath { get; set; } = "/student/fake-checkout";

    // Lets the fake gateway run outside Development (a staging host without Paymob keys). Production always refuses.
    public bool AllowFakePayments { get; set; }

    // Capped at 15 so the standard circuit breaker's 30 s sampling window stays at least twice the attempt timeout.
    [Range(1, 15)]
    public int AttemptTimeoutSeconds { get; set; } = 10;

    [Range(1, 120)]
    public int TotalTimeoutSeconds { get; set; } = 30;

    public PaymobOptions Paymob { get; set; } = new();
}
