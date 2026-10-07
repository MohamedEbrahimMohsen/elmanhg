using Core.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Elmanhg.Application.Shared.Observability;

public static class LogRedactor
{
    public const string EmailReplacement = "[redacted-email]";
    public const string PhoneReplacement = "[redacted-phone]";

    // Egyptian mobile numbers, local (01x) or international (+20 / 20). Separators are allowed only from a word start or +, in 4-4 or 1-3-4 groups, so GUIDs and number lists stay intact.
    private static readonly Regex PhonePattern = new(@"(\+?20)?0?1[0125][0-9]{8}\b|(?:\+|\b)(?:20[ -]?)?(?:0[ -]?)?1[ -]?[0125](?:[ -]?[0-9]{4}[ -]?[0-9]{4}|[0-9][ -]?[0-9]{3}[ -]?[0-9]{4})\b", RegexOptions.NonBacktracking);

    private static readonly TextRedactor Redactor = new([new(RedactionPatterns.EmailAddress, EmailReplacement), new(PhonePattern, PhoneReplacement)]);

    [return: NotNullIfNotNull(nameof(value))]
    public static string? Redact(string? value) => Redactor.Redact(value);
}
