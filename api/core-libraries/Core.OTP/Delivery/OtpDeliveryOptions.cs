using Core.OTP.Delivery.Email;
using Core.OTP.Delivery.Sms;
using Core.OTP.Delivery.WhatsApp;
using System.ComponentModel.DataAnnotations;

namespace Core.OTP.Delivery;

public sealed class OtpDeliveryOptions
{
    public const string SectionName = "OtpDelivery";

    [Required]
    public OtpChannel? DefaultPhoneChannel { get; set; }

    [Required]
    [RegularExpression("^[0-9]{1,4}$")]
    public string CountryCallingCode { get; set; } = string.Empty;

    // Capped at 15 so the standard circuit breaker's 30 s sampling window stays at least twice the attempt timeout.
    [Range(1, 15)]
    public int AttemptTimeoutSeconds { get; set; }

    [Range(1, 120)]
    public int TotalTimeoutSeconds { get; set; }

    public WhatsAppOtpOptions WhatsApp { get; set; } = new();

    public EmailOtpOptions Email { get; set; } = new();

    public SmsOtpOptions Sms { get; set; } = new();
}
