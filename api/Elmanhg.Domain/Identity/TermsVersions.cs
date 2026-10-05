namespace Elmanhg.Domain.Identity;

public static class TermsVersions
{
    // The terms text ships in the web bundle under this version; a new text is a new constant and a release, never a config change.
    public const string Current = "2026-10-05";

    private static readonly string[] Known = [Current];

    public static bool IsKnown(string? version) => version is not null && Known.Contains(version, StringComparer.Ordinal);
}
