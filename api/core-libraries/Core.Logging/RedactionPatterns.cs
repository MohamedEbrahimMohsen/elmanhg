using System.Text.RegularExpressions;

namespace Core.Logging;

public static class RedactionPatterns
{
    // Literal or URL-encoded (%40) email addresses; linear-time so it is safe on untrusted text.
    public static readonly Regex EmailAddress = new(@"[A-Za-z0-9._%+-]+(?:@|%40)[A-Za-z0-9.-]+\.[A-Za-z]{2,}", RegexOptions.NonBacktracking);
}
