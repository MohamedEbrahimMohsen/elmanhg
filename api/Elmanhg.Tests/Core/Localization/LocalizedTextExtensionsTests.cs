using Core.DDD.Models;
using Core.Localization;
using FluentAssertions;
using System.Globalization;

namespace Elmanhg.Tests.Core.Localization;

[Collection(DefaultThreadCultureCollection.Name)]
public sealed class LocalizedTextExtensionsTests
{
    private static readonly LocalizedText Text = new("مرحبا", "Hello");

    [Fact]
    public void Localized_EnglishCulture_ReturnsEnglish()
    {
        using var _ = new CultureScope("en-US");
        CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("ar");

        var value = Text.Localized();

        value.Should().Be("Hello");
    }

    [Fact]
    public void Localized_ArabicCulture_ReturnsArabic()
    {
        using var _ = new CultureScope("ar");
        CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en");

        var value = Text.Localized();

        value.Should().Be("مرحبا");
    }

    [Fact]
    public void Localized_UnsupportedCultureWithArabicThreadDefault_ReturnsArabic()
    {
        using var _ = new CultureScope("fr");
        CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("ar");

        var value = Text.Localized();

        value.Should().Be("مرحبا");
    }

    [Fact]
    public void Localized_ThreadDefaultChangedBetweenCalls_UsesCurrentDefault()
    {
        using var _ = new CultureScope("fr");
        CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("ar");
        var first = Text.Localized();
        CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en");

        var second = Text.Localized();

        (first, second).Should().Be(("مرحبا", "Hello"));
    }

    [Fact]
    public void Localized_NullTextUnsupportedCulture_ReturnsEmpty()
    {
        using var _ = new CultureScope("fr");
        CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("ar");

        var value = ((LocalizedText)null!).Localized();

        value.Should().BeEmpty();
    }
}
