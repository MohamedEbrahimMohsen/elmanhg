using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Elmanhg.Application.Shared.RichText;

namespace Elmanhg.Infrastructure.RichText;

public sealed class RichTextExtractor : IRichTextExtractor
{
    private const string DataTypeAttribute = "data-type";
    private const string LatexAttribute = "data-latex";
    private const string InlineMathType = "inline-math";
    private const string BlockMathType = "block-math";

    public IReadOnlyList<RichTextBlock> ExtractBlocks(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return [];
        }

        var document = new HtmlParser().ParseDocument(html);
        return document.Body!.ChildNodes
            .SelectMany(Blocks)
            .Where(x => x.Text.Length > 0)
            .ToList();
    }

    private static IEnumerable<RichTextBlock> Blocks(INode node) => node switch
    {
        IElement { LocalName: "h2" or "h3" } heading => [new RichTextBlock(RichTextBlockKind.Heading, Text(heading))],
        IElement { LocalName: "ul" } list => Items(list).Select(x => Paragraph($"- {Text(x)}")),
        IElement { LocalName: "ol" } list => Items(list).Select((x, index) => Paragraph($"{Start(list) + index}. {Text(x)}")),
        IElement { LocalName: "hr" } => [],
        IElement element when IsMath(element, BlockMathType) => [Paragraph(BlockMath(element))],
        IElement or IText => [Paragraph(Text(node))],
        _ => [],
    };

    private static string Text(INode node) => string.Join(' ', RawText(node).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string RawText(INode node) => node switch
    {
        IText text => text.Data,
        IElement element when IsMath(element, InlineMathType) => $"${element.GetAttribute(LatexAttribute)}$",
        IElement element when IsMath(element, BlockMathType) => BlockMath(element),
        IElement { LocalName: "img" } image => image.GetAttribute("alt") ?? string.Empty,
        IElement { LocalName: "br" } => " ",
        IElement element => string.Concat(element.ChildNodes.Select(RawText)),
        _ => string.Empty,
    };

    private static IEnumerable<IElement> Items(IElement list) => list.Children.Where(x => x.LocalName == "li");

    private static int Start(IElement list) => int.TryParse(list.GetAttribute("start"), out var start) ? start : 1;

    private static bool IsMath(IElement element, string type) => element.GetAttribute(DataTypeAttribute) == type;

    private static string BlockMath(IElement element) => $"$${element.GetAttribute(LatexAttribute)}$$";

    private static RichTextBlock Paragraph(string text) => new(RichTextBlockKind.Paragraph, text);
}
