using Core.Localization;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace Elmanhg.Tests.Core.Localization;

public sealed class LocalizationManagerTests
{
    private const string Arabic = "مرحبا";
    private const string English = "Hello";

    [Fact]
    public void GetLocalizedValue_UnsupportedCultureWithArabicDefault_ReturnsArabic()
    {
        var manager = new LocalizationManager(Configuration(new() { [DefaultLanguageResolver.ConfigurationKey] = "ar" }));
        using var _ = new CultureScope("fr");

        var value = manager.GetLocalizedValue(Arabic, English);

        value.Should().Be(Arabic);
    }

    [Fact]
    public void GetLocalizedValue_LegacyKeyOnly_FallsBackToEnglish()
    {
        var manager = new LocalizationManager(Configuration(new() { ["Localization:DefaultLanguage"] = "ar" }));
        using var _ = new CultureScope("fr");

        var value = manager.GetLocalizedValue(Arabic, English);

        value.Should().Be(English);
    }

    [Fact]
    public void GetLocalizedValue_SingleCharacterDefault_FallsBackToEnglish()
    {
        var manager = new LocalizationManager(Configuration(new() { [DefaultLanguageResolver.ConfigurationKey] = "a" }));
        using var _ = new CultureScope("fr");

        var value = manager.GetLocalizedValue(Arabic, English);

        value.Should().Be(English);
    }

    [Fact]
    public void GetLocalizedValue_ArabicCulture_ReturnsArabic()
    {
        var manager = new LocalizationManager(Configuration([]));
        using var _ = new CultureScope("ar-EG");

        var value = manager.GetLocalizedValue(Arabic, English);

        value.Should().Be(Arabic);
    }

    private static IConfiguration Configuration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
