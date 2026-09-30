using Elmanhg.Application.Questions.Shared;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Questions.Shared;

public sealed class RichTextContentTests
{
    [Fact]
    public void HasContent_Text_ReturnsTrue()
    {
        RichTextContent.HasContent("<p>x</p>").Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("<p></p>")]
    [InlineData("<p>&nbsp;</p>")]
    [InlineData("<p> <br></p>")]
    public void HasContent_MarkupOnly_ReturnsFalse(string? html)
    {
        RichTextContent.HasContent(html).Should().BeFalse();
    }

    [Theory]
    [InlineData("""<p><img src="/api/media/a.png" alt=""></p>""")]
    [InlineData("""<p><span data-type="inline-math" data-latex="x^2"></span></p>""")]
    public void HasContent_ImageOrFormula_ReturnsTrue(string html)
    {
        RichTextContent.HasContent(html).Should().BeTrue();
    }

    [Theory]
    [InlineData("<p>x</p>")]
    [InlineData("""<p><img src="/api/media/a.png" alt="Graph of v against t"></p>""")]
    [InlineData("""<p><img alt='Graph' src="/api/media/a.png"></p>""")]
    [InlineData("""<p><span data-type="inline-math" data-latex="x^2"></span></p>""")]
    public void HasGradableText_TextAltOrFormula_ReturnsTrue(string html)
    {
        RichTextContent.HasGradableText(html).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("<p> <br></p>")]
    [InlineData("""<p><img src="/api/media/a.png" alt=""></p>""")]
    [InlineData("""<p><img src="/api/media/a.png" alt="&nbsp;"></p>""")]
    [InlineData("""<p><img src="/api/media/a.png"></p>""")]
    public void HasGradableText_NoTextOrImageWithoutAlt_ReturnsFalse(string? html)
    {
        RichTextContent.HasGradableText(html).Should().BeFalse();
    }
}
