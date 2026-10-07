using Core.DDD.Models;
using System.Globalization;

namespace Core.Localization;

public static class LocalizedTextExtensions
{
    public static string Localized(this LocalizedText text)
    {
        var language = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
        var defaultLanguage = CultureInfo.DefaultThreadCurrentCulture?.TwoLetterISOLanguageName;

        return language switch
        {
            "en" => text?.English,
            "ar" => text?.Arabic,
            _ => defaultLanguage == "ar" ? text?.Arabic : text?.English,
        } ?? string.Empty;
    }
}
