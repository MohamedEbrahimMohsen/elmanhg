using System.ComponentModel.DataAnnotations;

namespace Elmanhg.Infrastructure.Sms;

public sealed class SmsOptions
{
    public const string SectionName = "Sms";

    [Required]
    public SmsProvider? Provider { get; set; }
}
