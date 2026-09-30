using System.Net;
using System.Text.RegularExpressions;

namespace Elmanhg.Application.Questions.Shared;

public static class RichTextContent
{
    private const string ImageMarker = "<img";
    private const string FormulaMarker = "data-latex";

    private static readonly Regex Tag = new("<[^<>]*>", RegexOptions.NonBacktracking);
    private static readonly Regex ImageAlt = new("""<img\b[^>]*\balt\s*=\s*(?:"(?<alt>[^"]*)"|'(?<alt>[^']*)'|(?<alt>[^\s"'>]+))""", RegexOptions.NonBacktracking | RegexOptions.IgnoreCase);

    // Mirrors web hasRichTextContent: text, an image or a formula counts.
    public static bool HasContent(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return false;
        }

        if (html.Contains(ImageMarker, StringComparison.OrdinalIgnoreCase) || html.Contains(FormulaMarker, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return HasVisibleText(html);
    }

    // Mirrors RichTextExtractor: an image only yields text through its alt, so an image without alt gives the AI grader nothing.
    public static bool HasGradableText(string? html) =>
        !string.IsNullOrWhiteSpace(html)
        && (html.Contains(FormulaMarker, StringComparison.OrdinalIgnoreCase) || HasVisibleText(html) || ImageAlt.Matches(html).Any(x => !string.IsNullOrWhiteSpace(WebUtility.HtmlDecode(x.Groups["alt"].Value))));

    private static bool HasVisibleText(string html) => !string.IsNullOrWhiteSpace(WebUtility.HtmlDecode(Tag.Replace(html, " ")));
}
