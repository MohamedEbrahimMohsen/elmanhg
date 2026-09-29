using Elmanhg.Application.Shared.RichText;
using Elmanhg.Domain.ContentRetrieval;

namespace Elmanhg.Application.ContentRetrieval.Shared;

public static class LessonContentChunker
{
    private const char WordSeparator = ' ';
    private const string PieceSeparator = "\n";

    public static List<LessonContentChunkDraft> Chunk(LessonContentSection section, Guid? questionId, int? questionVersion, IReadOnlyList<RichTextBlock> blocks, bool headingsAsTitles, int maxCharacters)
    {
        List<LessonContentChunkDraft> chunks = [];
        List<string> buffer = [];
        var bufferLength = 0;
        string? title = null;

        void Flush()
        {
            if (buffer.Count == 0)
            {
                return;
            }

            chunks.Add(new LessonContentChunkDraft(section, title, chunks.Count + 1, questionId, questionVersion, string.Join(PieceSeparator, buffer)));
            buffer.Clear();
            bufferLength = 0;
        }

        foreach (var block in blocks.Where(x => !string.IsNullOrWhiteSpace(x.Text)))
        {
            if (block.Kind == RichTextBlockKind.Heading && headingsAsTitles)
            {
                Flush();
                title = Truncate(block.Text, LessonContentChunk.SectionTitleMaxLength);
                continue;
            }

            foreach (var piece in Split(block.Text, maxCharacters))
            {
                if (buffer.Count > 0 && bufferLength + PieceSeparator.Length + piece.Length > maxCharacters)
                {
                    Flush();
                }

                bufferLength += buffer.Count == 0 ? piece.Length : PieceSeparator.Length + piece.Length;
                buffer.Add(piece);
            }
        }

        Flush();
        return chunks;
    }

    private static IEnumerable<string> Split(string text, int maxCharacters)
    {
        var remainder = text.Trim();
        while (remainder.Length > maxCharacters)
        {
            var cut = remainder.LastIndexOf(WordSeparator, maxCharacters);
            if (cut <= 0)
            {
                cut = maxCharacters;
            }

            var piece = remainder[..cut].Trim();
            if (piece.Length > 0)
            {
                yield return piece;
            }

            remainder = remainder[cut..].Trim();
        }

        if (remainder.Length > 0)
        {
            yield return remainder;
        }
    }

    private static string Truncate(string text, int maxLength) => text.Length <= maxLength ? text : text[..maxLength];
}
