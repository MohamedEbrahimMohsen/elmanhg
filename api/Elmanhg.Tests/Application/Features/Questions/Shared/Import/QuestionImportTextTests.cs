using Elmanhg.Application.Questions.Shared.Import;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.Questions.Shared.Import;

public sealed class QuestionImportTextTests
{
    [Fact]
    public void ToHtml_Null_ReturnsEmpty()
    {
        QuestionImportText.ToHtml(null).Should().BeEmpty();
    }

    [Fact]
    public void ToHtml_PlainText_EncodesAndWrapsInParagraph()
    {
        QuestionImportText.ToHtml("a < b & c").Should().Be("<p>a &lt; b &amp; c</p>");
    }

    [Fact]
    public void ToHtml_MultipleLines_OneParagraphPerNonBlankLine()
    {
        QuestionImportText.ToHtml("one\r\n\r\ntwo").Should().Be("<p>one</p><p>two</p>");
    }

    [Fact]
    public void ToHtml_DollarLatex_BecomesInlineMathSpan()
    {
        QuestionImportText.ToHtml("F = $m a$").Should().Be("<p>F = <span data-type=\"inline-math\" data-latex=\"m a\"></span></p>");
    }

    [Fact]
    public void ToHtml_LatexWithQuote_IsAttributeEncoded()
    {
        QuestionImportText.ToHtml("$a\"b$").Should().Contain("data-latex=\"a&quot;b\"");
    }

    [Fact]
    public void ToHtml_FillPlaceholder_IsKept()
    {
        QuestionImportText.ToHtml("v = [[1]] m/s").Should().Be("<p>v = [[1]] m/s</p>");
    }
}
