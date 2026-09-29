using Elmanhg.Application.Shared.RichText;
using Elmanhg.Infrastructure.RichText;
using FluentAssertions;

namespace Elmanhg.Tests.Infrastructure.RichText;

public sealed class RichTextExtractorTests
{
    private readonly RichTextExtractor _extractor = new();

    [Fact]
    public void ExtractBlocks_HeadingsAndParagraphs_ReturnsKindsInOrder()
    {
        var blocks = _extractor.ExtractBlocks("<h2>Ohm's law</h2><p>V = <strong>IR</strong></p><h3>Power</h3><blockquote><p>P = VI</p></blockquote><hr>");

        blocks.Should().Equal(new RichTextBlock(RichTextBlockKind.Heading, "Ohm's law"), new RichTextBlock(RichTextBlockKind.Paragraph, "V = IR"), new RichTextBlock(RichTextBlockKind.Heading, "Power"), new RichTextBlock(RichTextBlockKind.Paragraph, "P = VI"));
    }

    [Fact]
    public void ExtractBlocks_OrderedListWithStart_NumbersItems()
    {
        var blocks = _extractor.ExtractBlocks("<ol start=\"3\"><li><p>a</p></li><li>b</li></ol>");

        blocks.Select(x => x.Text).Should().Equal("3. a", "4. b");
    }

    [Fact]
    public void ExtractBlocks_BulletList_PrefixesDash()
    {
        var blocks = _extractor.ExtractBlocks("<ul><li>a</li><li>b</li></ul>");

        blocks.Should().Equal(new RichTextBlock(RichTextBlockKind.Paragraph, "- a"), new RichTextBlock(RichTextBlockKind.Paragraph, "- b"));
    }

    [Fact]
    public void ExtractBlocks_InlineAndBlockMath_EmitsLatexDelimiters()
    {
        var blocks = _extractor.ExtractBlocks("<p>Force <span data-type=\"inline-math\" data-latex=\"F=ma\"></span> holds</p><div data-type=\"block-math\" data-latex=\"x^2\"></div>");

        blocks.Select(x => x.Text).Should().Equal("Force $F=ma$ holds", "$$x^2$$");
    }

    [Fact]
    public void ExtractBlocks_ImageAndBreak_UsesAltAndCollapsesWhitespace()
    {
        var blocks = _extractor.ExtractBlocks("<p>  A <img src=\"/api/media/c.png\" alt=\"circuit\">line<br>next\n\n  end  </p>");

        blocks.Should().ContainSingle().Which.Text.Should().Be("A circuitline next end");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<p></p><p>   </p><hr>")]
    public void ExtractBlocks_NullOrBlankOrEmptyParagraphs_ReturnsNoBlocks(string? html)
    {
        var blocks = _extractor.ExtractBlocks(html);

        blocks.Should().BeEmpty();
    }
}
