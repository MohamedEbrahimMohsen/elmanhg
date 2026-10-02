using System.ComponentModel.DataAnnotations;

namespace Elmanhg.Infrastructure.Messaging;

public sealed class OutOfAppReminderOptions
{
    public const string SectionName = "OutOfAppReminders";
    public const string EnglishLanguage = "en";

    public string WhatsAppTemplateName { get; set; } = string.Empty;

    [Required]
    [RegularExpression("^[a-z]{2,3}(_[A-Z]{2})?$")]
    public string WhatsAppLanguageCode { get; set; } = "ar";

    public bool WhatsAppThreadButton { get; set; } = true;

    [Required]
    [RegularExpression("^(ar|en)$")]
    public string EmailLanguage { get; set; } = "ar";

    [Required]
    public string EmailSubjectArabic { get; set; } = "تذكير: سؤال طالب بانتظار ردك";

    [Required]
    public string EmailSubjectEnglish { get; set; } = "Reminder: a student question is waiting for your reply";

    public string ThreadLinkBaseUrl { get; set; } = string.Empty;

    [Required]
    public string TimeZone { get; set; } = "Africa/Cairo";

    public bool UsesEnglishEmail => EmailLanguage == EnglishLanguage;
}
