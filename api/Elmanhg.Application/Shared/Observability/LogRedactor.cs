using Core.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Elmanhg.Application.Shared.Observability;

public static class LogRedactor
{
    public const string EmailReplacement = "[redacted-email]";
    public const string PhoneReplacement = "[redacted-phone]";

    // ASCII, Arabic-Indic and Extended Arabic-Indic digits. Regular strings, not verbatim: the pattern holds the characters themselves, because .NET only knows \u0660 and RE2 only knows \x{0660}.
    private const string Digit = "[0-9\u0660-\u0669\u06F0-\u06F9]";
    private const string NotWordCharacter = "[^0-9\u0660-\u0669\u06F0-\u06F9A-Za-z_]";
    private const string Zero = "[0\u0660\u06F0]";
    private const string One = "[1\u0661\u06F1]";
    private const string Two = "[2\u0662\u06F2]";
    private const string OperatorDigit = "[0125\u0660\u0661\u0662\u0665\u06F0\u06F1\u06F2\u06F5]";

    // Egyptian mobile numbers, local (01x) or international (+20 / 0020 / 20), contiguous or with single spaces or hyphens in 4-4 or 1-3-4 groups, in any mix of the three digit scripts. Every number starts after +, the text start or a character that is not an ASCII letter, a digit or an underscore, so GUIDs, hex ids and longer digit runs stay intact.
    // No \b: .NET counts Arabic letters as word characters and RE2 does not. The boundaries are captured and written back, and the OTel collector runs this exact string (deploy/observability/otel-collector/config.yaml). ^ and $, not \A and \z: NonBacktracking drops the captures when \z meets a final newline.
    public const string PhonePattern = $@"(?:\+|(^|{NotWordCharacter})(?:{Zero}{Zero})?)(?:{Two}{Zero}[ -]?)?(?:{Zero}[ -]?)?{One}[ -]?{OperatorDigit}(?:[ -]?{Digit}{{4}}[ -]?{Digit}{{4}}|{Digit}[ -]?{Digit}{{3}}[ -]?{Digit}{{4}})({NotWordCharacter}|$)";

    public const string PhoneReplacementPattern = "${1}" + PhoneReplacement + "${2}";

    private static readonly Regex PhoneRegex = new(PhonePattern, RegexOptions.NonBacktracking);

    // Each match consumes the character after the number, so a number right behind another one is only found by the second pass.
    private static readonly TextRedactor Redactor = new([new(RedactionPatterns.EmailAddress, EmailReplacement), new(PhoneRegex, PhoneReplacementPattern), new(PhoneRegex, PhoneReplacementPattern)]);

    [return: NotNullIfNotNull(nameof(value))]
    public static string? Redact(string? value) => Redactor.Redact(value);
}
