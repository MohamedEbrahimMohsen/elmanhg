using Elmanhg.Infrastructure.RichText;
using Elmanhg.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Elmanhg.Tests.Infrastructure.RichText;

public sealed class RichTextSanitizerTests
{
    private readonly RichTextSanitizer _sanitizer = new(Options.Create(new FileStorageOptions { Provider = FileStorageProvider.Local, LocalRootPath = "media", PublicBaseUrl = "/api/media" }));

    [Fact]
    public void Sanitize_ScriptTag_RemovesIt()
    {
        var result = _sanitizer.Sanitize("<p>Hi</p><script>alert(1)</script>");

        result.Should().NotContain("<script").And.NotContain("alert");
    }

    [Fact]
    public void Sanitize_EventHandlerAttribute_RemovesIt()
    {
        var result = _sanitizer.Sanitize("<p onclick=\"x()\">Hi</p>");

        result.Should().Be("<p>Hi</p>");
    }

    [Fact]
    public void Sanitize_JavascriptLink_RemovesHref()
    {
        var result = _sanitizer.Sanitize("<p><a href=\"javascript:alert(1)\">Click</a></p>");

        result.Should().NotContain("javascript:");
    }

    [Fact]
    public void Sanitize_StyleAndClassAttributes_RemovesThem()
    {
        var result = _sanitizer.Sanitize("<p style=\"color: red\" class=\"x\">Hi</p>");

        result.Should().NotContain("style=").And.NotContain("class=");
    }

    [Fact]
    public void Sanitize_AllowedFormatting_KeepsMarkup()
    {
        const string html = "<h2>Title</h2><p><strong>Bold</strong></p><ul><li>Item</li></ul><p><a href=\"https://example.com\">Link</a></p>";

        var result = _sanitizer.Sanitize(html);

        result.Should().Contain("<h2>Title</h2>").And.Contain("<strong>Bold</strong>").And.Contain("<ul><li>Item</li></ul>").And.Contain("<a href=\"https://example.com\">Link</a>");
    }

    [Fact]
    public void Sanitize_MathNodes_KeepsLatexAttributes()
    {
        const string html = "<p><span data-type=\"inline-math\" data-latex=\"\\frac{a}{b}\"></span></p><div data-type=\"block-math\" data-latex=\"\\frac{a}{b}\"></div>";

        var result = _sanitizer.Sanitize(html);

        result.Should().Contain("<span data-type=\"inline-math\" data-latex=\"\\frac{a}{b}\"></span>").And.Contain("<div data-type=\"block-math\" data-latex=\"\\frac{a}{b}\"></div>");
    }

    [Fact]
    public void Sanitize_ImageFromStorage_KeepsSourceAndAlt()
    {
        var result = _sanitizer.Sanitize("<img src=\"/api/media/lessons/x.png\" alt=\"Force diagram\">");

        result.Should().Contain("src=\"/api/media/lessons/x.png\"").And.Contain("alt=\"Force diagram\"");
    }

    [Fact]
    public void Sanitize_ExternalImage_RemovesSource()
    {
        var result = _sanitizer.Sanitize("<img src=\"https://evil.example/pixel.png\" alt=\"Pixel\">");

        result.Should().NotContain("evil.example");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void Sanitize_BlankInput_ReturnsEmpty(string? html)
    {
        var result = _sanitizer.Sanitize(html);

        result.Should().BeEmpty();
    }
}
