namespace Core.Localization;

public static class DefaultLanguageResolver
{
    public const string ConfigurationKey = "CoreLocalization:DefaultLanguage";
    public const string Fallback = "en";

    public static string Resolve(string? configured)
    {
        var value = configured?.Trim();
        return value is { Length: >= 2 } ? value[..2].ToLowerInvariant() : Fallback;
    }
}
