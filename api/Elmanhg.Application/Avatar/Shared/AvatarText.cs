using Elmanhg.Application.Shared.RichText;

namespace Elmanhg.Application.Avatar.Shared;

public static class AvatarText
{
    public static string Plain(string? html, IRichTextExtractor extractor, int maxLength)
    {
        var text = string.Join("\n", extractor.ExtractBlocks(html).Select(x => x.Text));
        return Truncate(text, maxLength);
    }

    public static string Truncate(string text, int maxLength) => text.Length <= maxLength ? text : text[..maxLength];

    public static string RetrievalQuery(string message, string? stem, int maxLength)
    {
        var query = stem is null ? message.Trim() : stem + "\n" + message.Trim();
        return Truncate(query, maxLength);
    }
}
