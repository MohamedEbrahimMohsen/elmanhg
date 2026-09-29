using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Elmanhg.Application.Shared.Observability;

public static class LogRedactor
{
    public const string EmailReplacement = "[redacted-email]";
    public const string PhoneReplacement = "[redacted-phone]";

    private static readonly Regex EmailPattern = new(@"[A-Za-z0-9._%+-]+(?:@|%40)[A-Za-z0-9.-]+\.[A-Za-z]{2,}", RegexOptions.NonBacktracking);

    // Egyptian mobile numbers, local (01x) or international (+20 / 20).
    private static readonly Regex PhonePattern = new(@"(\+?20)?0?1[0125][0-9]{8}\b", RegexOptions.NonBacktracking);

    [return: NotNullIfNotNull(nameof(value))]
    public static string? Redact(string? value) => value is null ? null : PhonePattern.Replace(EmailPattern.Replace(value, EmailReplacement), PhoneReplacement);
}
