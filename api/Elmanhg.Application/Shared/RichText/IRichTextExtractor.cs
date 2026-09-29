namespace Elmanhg.Application.Shared.RichText;

public interface IRichTextExtractor
{
    IReadOnlyList<RichTextBlock> ExtractBlocks(string? html);
}
