using Elmanhg.Application.Shared.Messaging;
using System.Net;

namespace Elmanhg.Infrastructure.Messaging;

public static class TeacherReminderEmailTemplate
{
    private const string NamePlaceholder = "{{name}}";
    private const string SubjectPlaceholder = "{{subject}}";
    private const string LessonPlaceholder = "{{lesson}}";
    private const string DeadlinePlaceholder = "{{deadline}}";
    private const string LinkPlaceholder = "{{link}}";
    private const string ResourcePrefix = "Elmanhg.Infrastructure.Messaging.Templates.";

    private static readonly Lazy<string> ArabicHtml = new(() => Read("TeacherReminderEmail.ar.html"));
    private static readonly Lazy<string> ArabicText = new(() => Read("TeacherReminderEmail.ar.txt"));
    private static readonly Lazy<string> EnglishHtml = new(() => Read("TeacherReminderEmail.en.html"));
    private static readonly Lazy<string> EnglishText = new(() => Read("TeacherReminderEmail.en.txt"));

    public static string RenderHtml(string language, TeacherThreadReminderMessage reminder, string deadline, string link) => Render(IsEnglish(language) ? EnglishHtml.Value : ArabicHtml.Value, reminder, deadline, link, WebUtility.HtmlEncode);

    public static string RenderText(string language, TeacherThreadReminderMessage reminder, string deadline, string link) => Render(IsEnglish(language) ? EnglishText.Value : ArabicText.Value, reminder, deadline, link, value => value);

    private static bool IsEnglish(string language) => language == OutOfAppReminderOptions.EnglishLanguage;

    private static string Render(string template, TeacherThreadReminderMessage reminder, string deadline, string link, Func<string, string> encode) => template
        .Replace(NamePlaceholder, encode(reminder.TeacherName), StringComparison.Ordinal)
        .Replace(SubjectPlaceholder, encode(reminder.SubjectName), StringComparison.Ordinal)
        .Replace(LessonPlaceholder, encode(reminder.LessonName), StringComparison.Ordinal)
        .Replace(DeadlinePlaceholder, encode(deadline), StringComparison.Ordinal)
        .Replace(LinkPlaceholder, encode(link), StringComparison.Ordinal);

    private static string Read(string fileName)
    {
        using var stream = typeof(TeacherReminderEmailTemplate).Assembly.GetManifestResourceStream(ResourcePrefix + fileName) ?? throw new InvalidOperationException($"Missing embedded template {fileName}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
