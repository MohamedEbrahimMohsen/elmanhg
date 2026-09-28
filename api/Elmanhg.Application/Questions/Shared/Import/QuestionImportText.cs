using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Elmanhg.Application.Questions.Shared.Import;

public static class QuestionImportText
{
    // $…$ in a cell is the admin's inline-math shorthand; storage keeps LaTeX in data-latex only (docs/rich-text.md).
    private static readonly Regex InlineMath = new(@"\$([^$\n]+)\$", RegexOptions.NonBacktracking);

    public static string ToHtml(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var paragraphs = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => $"<p>{ConvertLine(x.Trim())}</p>");
        return string.Concat(paragraphs);
    }

    private static string ConvertLine(string line)
    {
        var builder = new StringBuilder();
        var position = 0;
        foreach (Match match in InlineMath.Matches(line))
        {
            builder.Append(WebUtility.HtmlEncode(line[position..match.Index]));
            builder.Append($"<span data-type=\"inline-math\" data-latex=\"{WebUtility.HtmlEncode(match.Groups[1].Value.Trim())}\"></span>");
            position = match.Index + match.Length;
        }

        builder.Append(WebUtility.HtmlEncode(line[position..]));
        return builder.ToString();
    }
}
