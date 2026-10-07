using Core.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Elmanhg.Application.Shared.Observability;

public static class LogRedactor
{
    public const string EmailReplacement = "[redacted-email]";
    public const string PhoneReplacement = "[redacted-phone]";

    // Egyptian mobile numbers, local (01x) or international (+20 / 20). Separators are allowed only after +, the text start or a character that is not an ASCII letter, digit or underscore, in 4-4 or 1-3-4 groups, so GUIDs and number lists stay intact.
    // No \b: .NET counts Arabic letters as word characters and RE2 does not. The ASCII boundaries are captured and written back, and the OTel collector runs this exact string (deploy/observability/otel-collector/config.yaml). ^ and $, not \A and \z: NonBacktracking drops the captures when \z meets a final newline.
    public const string PhonePattern = @"(?:(?:\+?20)?0?1[0125][0-9]{8}|(?:\+|(^|[^0-9A-Za-z_]))(?:20[ -]?)?(?:0[ -]?)?1[ -]?[0125](?:[ -]?[0-9]{4}[ -]?[0-9]{4}|[0-9][ -]?[0-9]{3}[ -]?[0-9]{4}))([^0-9A-Za-z_]|$)";

    public const string PhoneReplacementPattern = "${1}" + PhoneReplacement + "${2}";

    private static readonly Regex PhoneRegex = new(PhonePattern, RegexOptions.NonBacktracking);

    // Each match consumes the character after the number, so a number right behind another one is only found by the second pass.
    private static readonly TextRedactor Redactor = new([new(RedactionPatterns.EmailAddress, EmailReplacement), new(PhoneRegex, PhoneReplacementPattern), new(PhoneRegex, PhoneReplacementPattern)]);

    [return: NotNullIfNotNull(nameof(value))]
    public static string? Redact(string? value) => Redactor.Redact(value);
}
