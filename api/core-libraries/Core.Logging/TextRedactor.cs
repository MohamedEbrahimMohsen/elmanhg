using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Core.Logging;

public sealed record RedactionRule(Regex Pattern, string Replacement);

public sealed class TextRedactor(IReadOnlyList<RedactionRule> rules)
{
    [return: NotNullIfNotNull(nameof(value))]
    public string? Redact(string? value) => value is null ? null : rules.Aggregate(value, (text, rule) => rule.Pattern.Replace(text, rule.Replacement));
}
