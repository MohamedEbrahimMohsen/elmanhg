using Core.Localization;
using FluentAssertions;

namespace Elmanhg.Tests.Core.Localization;

public sealed class DefaultLanguageResolverTests
{
    [Theory]
    [InlineData("ar", "ar")]
    [InlineData("ar-EG", "ar")]
    [InlineData("AR", "ar")]
    [InlineData(" en ", "en")]
    [InlineData(null, "en")]
    [InlineData("", "en")]
    [InlineData("a", "en")]
    public void Resolve_ConfiguredValue_ReturnsTwoLetterOrFallback(string? configured, string expected)
    {
        var language = DefaultLanguageResolver.Resolve(configured);

        language.Should().Be(expected);
    }
}
