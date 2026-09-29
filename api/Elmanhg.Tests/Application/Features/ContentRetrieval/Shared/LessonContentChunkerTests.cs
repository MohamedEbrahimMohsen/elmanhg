using Elmanhg.Application.ContentRetrieval.Shared;
using Elmanhg.Application.Shared.RichText;
using Elmanhg.Domain.ContentRetrieval;
using FluentAssertions;

namespace Elmanhg.Tests.Application.Features.ContentRetrieval.Shared;

public sealed class LessonContentChunkerTests
{
    private const int Max = 200;

    [Fact]
    public void Chunk_Heading_StartsNewChunkWithTitle()
    {
        RichTextBlock[] blocks = [Paragraph("intro"), Heading("Ohm's law"), Paragraph("body")];

        var chunks = Chunk(blocks, headingsAsTitles: true);

        chunks.Should().HaveCount(2);
        chunks[0].Should().Be(new LessonContentChunkDraft(LessonContentSection.Explanation, null, 1, null, null, "intro"));
        chunks[1].Should().Be(new LessonContentChunkDraft(LessonContentSection.Explanation, "Ohm's law", 2, null, null, "body"));
    }

    [Fact]
    public void Chunk_SmallParagraphs_MergedUpToMax()
    {
        var chunks = Chunk([Paragraph("first"), Paragraph("second"), Paragraph("third")], headingsAsTitles: true);

        chunks.Should().ContainSingle().Which.Content.Should().Be("first\nsecond\nthird");
    }

    [Fact]
    public void Chunk_ParagraphsOverMax_StartsNextChunk()
    {
        var first = new string('a', 100);
        var fits = new string('b', Max - first.Length - 1);
        var overflows = new string('c', Max - first.Length);

        var exact = Chunk([Paragraph(first), Paragraph(fits)], headingsAsTitles: true);
        var over = Chunk([Paragraph(first), Paragraph(overflows)], headingsAsTitles: true);

        exact.Should().ContainSingle().Which.Content.Length.Should().Be(Max);
        over.Select(x => x.Content).Should().Equal(first, overflows);
        over.Select(x => x.Position).Should().Equal(1, 2);
    }

    [Fact]
    public void Chunk_LongParagraph_SplitsAtWhitespace()
    {
        var words = Enumerable.Range(0, 120)
            .Select(x => $"word{x}")
            .ToList();

        var chunks = Chunk([Paragraph(string.Join(' ', words))], headingsAsTitles: true);

        chunks.Should().HaveCountGreaterThan(1);
        chunks.Should().OnlyContain(x => x.Content.Length <= Max);
        chunks.SelectMany(x => x.Content.Split([' ', '\n'])).Should().Equal(words);
    }

    [Fact]
    public void Chunk_UnbrokenText_HardSplitsAtMax()
    {
        var text = new string('x', Max * 2 + 17);

        var chunks = Chunk([Paragraph(text)], headingsAsTitles: true);

        chunks.Select(x => x.Content.Length).Should().Equal(Max, Max, 17);
    }

    [Fact]
    public void Chunk_HeadingsAsTitlesFalse_TreatsHeadingAsText()
    {
        var chunks = Chunk([Heading("Stem"), Paragraph("Explanation")], headingsAsTitles: false);

        var chunk = chunks.Should().ContainSingle().Which;
        chunk.SectionTitle.Should().BeNull();
        chunk.Content.Should().Be("Stem\nExplanation");
    }

    [Fact]
    public void Chunk_LongHeading_TruncatesTitleTo200()
    {
        var chunks = Chunk([Heading(new string('h', 250)), Paragraph("body")], headingsAsTitles: true);

        chunks.Should().ContainSingle().Which.SectionTitle!.Length.Should().Be(200);
    }

    [Fact]
    public void Chunk_NoBlocks_ReturnsEmpty()
    {
        var chunks = Chunk([], headingsAsTitles: true);

        chunks.Should().BeEmpty();
    }

    private static List<LessonContentChunkDraft> Chunk(IReadOnlyList<RichTextBlock> blocks, bool headingsAsTitles) => LessonContentChunker.Chunk(LessonContentSection.Explanation, null, null, blocks, headingsAsTitles, Max);

    private static RichTextBlock Paragraph(string text) => new(RichTextBlockKind.Paragraph, text);

    private static RichTextBlock Heading(string text) => new(RichTextBlockKind.Heading, text);
}
