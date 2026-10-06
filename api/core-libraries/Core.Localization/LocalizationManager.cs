using Microsoft.Extensions.Configuration;
using System.Globalization;

namespace Core.Localization;

public sealed class LocalizationManager(IConfiguration configuration) : ILocalizationManager
{
    private readonly string _defaultLanguage = DefaultLanguageResolver.Resolve(configuration[DefaultLanguageResolver.ConfigurationKey]);

    public T GetLocalizedValue<T>(T valueAr, T valueEn)
    {
        var currentLang = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;

        if (currentLang == "en")
            return valueEn;

        if (currentLang == "ar")
            return valueAr;

        return _defaultLanguage == "ar" ? valueAr : valueEn;
    }
}